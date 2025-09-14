using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MonitoringApplication.Data;
using MonitoringApplication.Hubs;
using MonitoringApplication.Models;
using System.Net.Sockets;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringApplication.Services
{
    public class HealthCheckService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningServices = new();
        private const int FAILURE_THRESHOLD = 3;

        public HealthCheckService(IServiceProvider serviceProvider, ILogger<HealthCheckService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("HealthCheckService is initializing and loading existing services.");

            // Register a callback for when the application is stopping
            stoppingToken.Register(() =>
                _logger.LogInformation("HealthCheckService is stopping."));

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var services = await dbContext.Services
                    .Where(s => !s.IsDeleted && !s.IsInMaintenance)
                    .ToListAsync(stoppingToken);

                foreach (var service in services)
                {
                    // Pass the application's main stoppingToken to the check loop
                    AddOrUpdateServiceCheck(service, stoppingToken);
                }
            }
            _logger.LogInformation($"Initialized {_runningServices.Count} service checks.");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        // Pass the application's stopping token here
        public void AddOrUpdateServiceCheck(MonitoredService service, CancellationToken stoppingToken = default)
        {
            if (_runningServices.TryRemove(service.Id, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
                _logger.LogInformation("Stopped existing check for updated service '{Name}'.", service.Name);
            }

            // Link the service-specific token with the application's main stopping token
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            if (_runningServices.TryAdd(service.Id, cts))
            {
                Task.Run(() => RunServiceCheckLoopAsync(service.Id, cts.Token), cts.Token);
                _logger.LogInformation("Started new check for service '{Name}' with interval {Interval}ms.", service.Name, service.RefreshIntervalMilliseconds);
            }
        }

        public void RemoveServiceCheck(string serviceId)
        {
            if (_runningServices.TryRemove(serviceId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _logger.LogInformation("Removed check for deleted service with ID '{ServiceId}'.", serviceId);
            }
        }

        private async Task RunServiceCheckLoopAsync(string serviceId, CancellationToken cancellationToken)
        {
            long nextDelay = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (nextDelay > 0)
                    {
                        await Task.Delay((int)nextDelay, cancellationToken);
                    }

                    // Throw if cancellation is requested during the delay
                    cancellationToken.ThrowIfCancellationRequested();

                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<StatusHub>>();

                    var service = await dbContext.Services.FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);

                    if (service == null || service.IsDeleted || service.IsInMaintenance)
                    {
                        _logger.LogWarning("Service with ID '{ServiceId}' is no longer valid for checking. Stopping loop.", serviceId);
                        RemoveServiceCheck(serviceId);
                        return;
                    }

                    // --- Health Check and Logic ---
                    var statusBeforeCheck = service.Status;
                    await PerformHealthCheck(service, cancellationToken);
                    var statusAfterCheck = service.Status;

                    if (statusAfterCheck == ServiceStatus.Online)
                    {
                        service.FailedCheckCount = 0;
                        if (statusBeforeCheck != ServiceStatus.Online)
                        {
                            var lastDowntime = await dbContext.DowntimeEvents
                               .Where(d => d.MonitoredServiceId == service.Id && d.EndTime == null)
                               .FirstOrDefaultAsync(cancellationToken);
                            if (lastDowntime != null) lastDowntime.EndTime = DateTime.UtcNow;
                        }
                    }
                    else // It's Offline
                    {
                        service.FailedCheckCount++;
                        if (statusBeforeCheck != ServiceStatus.Offline)
                        {
                            var newDowntimeEvent = new DowntimeEvent { StartTime = DateTime.UtcNow, MonitoredServiceId = service.Id };
                            await dbContext.DowntimeEvents.AddAsync(newDowntimeEvent, cancellationToken);
                        }
                    }

                    // --- Determine Next Delay ---
                    if (service.FailedCheckCount >= FAILURE_THRESHOLD)
                    {
                        _logger.LogWarning("Service '{Name}' failed {Count} checks. Waiting for retry interval of {Interval}ms.", service.Name, service.FailedCheckCount, service.RetryIntervalMilliseconds);
                        service.Status = ServiceStatus.Pending;
                        nextDelay = service.RetryIntervalMilliseconds;
                        service.FailedCheckCount = 0;
                    }
                    else
                    {
                        nextDelay = service.RefreshIntervalMilliseconds;
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    var serviceToBroadcast = await dbContext.Services
                        .AsNoTracking()
                        .Include(s => s.ServiceGroup)
                        .FirstOrDefaultAsync(s => s.Id == service.Id, cancellationToken);

                    if (serviceToBroadcast != null)
                    {
                        await hubContext.Clients.All.SendAsync("ReceiveServiceUpdate", serviceToBroadcast, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // This is expected when the application is shutting down.
                    _logger.LogInformation("Service check loop for service ID {ServiceId} was canceled.", serviceId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred in the check loop for service ID {ServiceId}.", serviceId);
                    // Wait for a short period before retrying to prevent rapid-fire error loops.
                    await Task.Delay(5000, cancellationToken);
                }
            }
            _logger.LogDebug("Check loop gracefully stopped for service ID '{ServiceId}'.", serviceId);
        }

        public async Task PerformHealthCheck(MonitoredService service, CancellationToken cancellationToken)
        {
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            try
            {
                switch (service.Type)
                {
                    case ServiceType.Website:
                    case ServiceType.Api:
                        if (!Uri.IsWellFormedUriString(service.Url, UriKind.Absolute))
                        {
                            service.Status = ServiceStatus.Offline;
                            service.LastStatusDescription = "URL نامعتبر است. آدرس باید کامل باشد (مثال: http://example.com).";
                            break;
                        }
                        using (var httpClient = httpClientFactory.CreateClient())
                        {
                            httpClient.Timeout = TimeSpan.FromSeconds(15);
                            var response = await httpClient.GetAsync(service.Url, cancellationToken);
                            service.Status = response.IsSuccessStatusCode ? ServiceStatus.Online : ServiceStatus.Offline;
                            service.LastStatusDescription = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                        }
                        break;
                    case ServiceType.TcpConnection:
                        var parts = service.Url.Split(':');
                        if (parts.Length < 2 || !int.TryParse(parts[^1], out var port))
                        {
                            service.Status = ServiceStatus.Offline;
                            service.LastStatusDescription = "آدرس نامعتبر است. باید به فرمت hostname:port باشد.";
                            break;
                        }
                        var host = string.Join(":", parts.Take(parts.Length - 1));
                        using (var tcpClient = new TcpClient())
                        {
                            try
                            {
                                // Use CancellationToken with ConnectAsync (available in .NET 6+)
                                await tcpClient.ConnectAsync(host, port, cancellationToken);
                                service.Status = ServiceStatus.Online;
                                service.LastStatusDescription = "اتصال با موفقیت برقرار شد.";
                            }
                            catch (OperationCanceledException)
                            {
                                service.Status = ServiceStatus.Offline;
                                service.LastStatusDescription = "اتصال برقرار نشد (Timeout).";
                            }
                            catch
                            {
                                service.Status = ServiceStatus.Offline;
                                service.LastStatusDescription = "اتصال برقرار نشد (Connection Refused).";
                            }
                        }
                        break;
                    case ServiceType.Redis:
                        try
                        {
                            var config = new ConfigurationOptions
                            {
                                EndPoints = { service.Url },
                                User = service.RedisUsername,
                                Password = string.IsNullOrEmpty(service.RedisPassword) ? null : service.RedisPassword,
                                AbortOnConnectFail = false,
                                ConnectTimeout = 10000
                            };
                            // Redis connection doesn't directly support CancellationToken in ConnectAsync
                            // but will be implicitly handled by the overall task cancellation.
                            using (var connection = await ConnectionMultiplexer.ConnectAsync(config))
                            {
                                var db = connection.GetDatabase(service.RedisDbNumber ?? -1);
                                await db.PingAsync();
                                service.Status = ServiceStatus.Online;
                                service.LastStatusDescription = "اتصال و پینگ Redis موفقیت‌آمیز بود.";
                            }
                        }
                        catch (Exception ex)
                        {
                            service.Status = ServiceStatus.Offline;
                            service.LastStatusDescription = $"اتصال به Redis ناموفق بود: {ex.Message}";
                        }
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                service.Status = ServiceStatus.Offline;
                service.LastStatusDescription = "عملیات بررسی لغو شد.";
                _logger.LogWarning("Health check for {Name} was canceled.", service.Name);
            }
            catch (Exception ex)
            {
                service.Status = ServiceStatus.Offline;
                service.LastStatusDescription = $"خطای غیرمنتظره: {ex.Message}";
                _logger.LogError(ex, "Error checking service {Name}", service.Name);
            }
            finally
            {
                service.LastCheckTime = DateTime.Now;
            }
        }
    }
}

