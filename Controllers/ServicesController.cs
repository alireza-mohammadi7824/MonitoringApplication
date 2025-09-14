using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitoringApplication.Data;
using MonitoringApplication.Hubs;
using MonitoringApplication.Models;
using Microsoft.AspNetCore.SignalR;
using MonitoringApplication.Services;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServicesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServicesController> _logger;
        private readonly HealthCheckService _healthCheckService;

        public ServicesController(ApplicationDbContext context, ILogger<ServicesController> logger, HealthCheckService healthCheckService)
        {
            _context = context;
            _logger = logger;
            _healthCheckService = healthCheckService;
        }

        [HttpGet]
        public async Task<ActionResult<System.Collections.Generic.IEnumerable<MonitoredService>>> GetServices()
        {
            var services = await _context.Services
                .Where(s => !s.IsDeleted)
                .Include(s => s.ServiceGroup)
                .ToListAsync();

            // This approach is safe because the `Type` property on the model handles invalid enum values.
            return Ok(services.OrderBy(s => s.ServiceGroup?.Name).ThenBy(s => s.SortOrder).ThenBy(s => s.Name));
        }

        [HttpGet("{id}/details")]
        public async Task<ActionResult<ServiceDetailsDto>> GetServiceDetails(string id)
        {
            var service = await _context.Services
                .Include(s => s.DowntimeHistory.OrderByDescending(d => d.StartTime).Take(10))
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (service == null) return NotFound();

            var serviceDetails = new ServiceDetailsDto
            {
                Id = service.Id,
                Name = service.Name,
                Url = service.Url,
                Type = service.Type,
                Status = service.Status,
                LastCheckTime = service.LastCheckTime,
                LastStatusDescription = service.LastStatusDescription,
                DowntimeHistory = service.DowntimeHistory.Select(d => new DowntimeEventDto
                {
                    Id = d.Id.ToString(),
                    StartTime = d.StartTime,
                    EndTime = d.EndTime
                }).ToList()
            };

            return Ok(serviceDetails);
        }

        [HttpPost("validate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ValidateServiceResponse>> ValidateService([FromBody] ValidateServiceRequest request)
        {
            var result = new ValidateServiceResponse();
            var tempService = new MonitoredService
            {
                Url = request.Url,
                Type = request.Type,
                RedisUsername = request.RedisUsername,
                RedisPassword = request.RedisPassword,
                RedisDbNumber = request.RedisDbNumber
            };

            // We can directly call the public PerformHealthCheck method on the singleton service
            await _healthCheckService.PerformHealthCheck(tempService, CancellationToken.None);

            result.IsOnline = tempService.Status == ServiceStatus.Online;
            result.StatusDescription = tempService.LastStatusDescription;

            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutMonitoredService(string id, [FromBody] CreateUpdateServiceDto dto)
        {
            var serviceToUpdate = await _context.Services.FindAsync(id);
            if (serviceToUpdate == null) return NotFound();

            serviceToUpdate.Name = dto.Name;
            serviceToUpdate.Url = dto.Url;
            serviceToUpdate.Type = dto.Type;
            serviceToUpdate.RefreshIntervalMilliseconds = dto.RefreshIntervalMilliseconds;
            serviceToUpdate.RetryIntervalMilliseconds = dto.RetryIntervalMilliseconds;
            serviceToUpdate.SortOrder = dto.SortOrder;
            serviceToUpdate.ServiceGroupId = dto.ServiceGroupId == 0 ? null : dto.ServiceGroupId;
            serviceToUpdate.RedisUsername = dto.RedisUsername;
            serviceToUpdate.RedisPassword = dto.RedisPassword;
            serviceToUpdate.RedisDbNumber = dto.RedisDbNumber;
            serviceToUpdate.Status = ServiceStatus.Pending;
            serviceToUpdate.FailedCheckCount = 0; // Reset count on update

            await _context.SaveChangesAsync();

            // Notify the HealthCheckService to restart the check for this service
            _healthCheckService.AddOrUpdateServiceCheck(serviceToUpdate);

            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<MonitoredService>> PostMonitoredService([FromBody] CreateUpdateServiceDto dto)
        {
            var monitoredService = new MonitoredService
            {
                Name = dto.Name,
                Url = dto.Url,
                Type = dto.Type,
                RefreshIntervalMilliseconds = dto.RefreshIntervalMilliseconds,
                RetryIntervalMilliseconds = dto.RetryIntervalMilliseconds,
                SortOrder = dto.SortOrder,
                ServiceGroupId = dto.ServiceGroupId == 0 ? null : dto.ServiceGroupId,
                RedisUsername = dto.RedisUsername,
                RedisPassword = dto.RedisPassword,
                RedisDbNumber = dto.RedisDbNumber,
                Status = ServiceStatus.Pending
            };

            _context.Services.Add(monitoredService);
            await _context.SaveChangesAsync();

            // Notify the HealthCheckService to start checking this new service
            _healthCheckService.AddOrUpdateServiceCheck(monitoredService);

            var createdService = await _context.Services
                .Include(s => s.ServiceGroup)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == monitoredService.Id);

            return CreatedAtAction(nameof(GetServices), new { id = monitoredService.Id }, createdService);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMonitoredService(string id)
        {
            var monitoredService = await _context.Services.FindAsync(id);
            if (monitoredService == null) return NotFound();

            monitoredService.IsDeleted = true;
            await _context.SaveChangesAsync();

            // Notify the HealthCheckService to stop checking this service
            _healthCheckService.RemoveServiceCheck(id);

            return NoContent();
        }
    }
}

