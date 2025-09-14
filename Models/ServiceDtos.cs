using System.ComponentModel.DataAnnotations;

namespace MonitoringApplication.Models
{
    public class CreateUpdateServiceDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty;

        [Required]
        public ServiceType Type { get; set; }

        public int RefreshIntervalMilliseconds { get; set; } = 60000;

        // NEW: Added RetryIntervalMilliseconds to the DTO
        public int RetryIntervalMilliseconds { get; set; } = 300000;

        public int SortOrder { get; set; } = 100;

        public int? ServiceGroupId { get; set; }

        public string? RedisUsername { get; set; }
        public string? RedisPassword { get; set; }
        public int? RedisDbNumber { get; set; }
    }

    public class ValidateServiceRequest
    {
        [Required]
        public string Url { get; set; } = string.Empty;

        [Required]
        public ServiceType Type { get; set; }

        public string? RedisUsername { get; set; }
        public string? RedisPassword { get; set; }
        public int? RedisDbNumber { get; set; }
    }

    public class ValidateServiceResponse
    {
        public bool IsOnline { get; set; }
        public string StatusDescription { get; set; } = string.Empty;
    }

    public class DowntimeEventDto
    {
        public string Id { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class ServiceDetailsDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public ServiceType Type { get; set; }
        public ServiceStatus Status { get; set; }
        public DateTime LastCheckTime { get; set; }
        public string? LastStatusDescription { get; set; }
        public List<DowntimeEventDto> DowntimeHistory { get; set; } = new();
    }
}

