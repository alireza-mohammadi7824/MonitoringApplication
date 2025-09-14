using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitoringApplication.Models
{
    public enum ServiceType
    {
        Website,
        Api,
        TcpConnection,
        Redis
    }

    public enum ServiceStatus
    {
        Pending,
        Online,
        Offline
    }

    public class MonitoredService
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty;

        [Required]
        public ServiceType Type { get; set; }

        public ServiceStatus Status { get; set; }

        public DateTime LastCheckTime { get; set; }

        public string? LastStatusDescription { get; set; }

        public int RefreshIntervalMilliseconds { get; set; } = 60000;

        // NEW: The interval to wait after 3 consecutive failures before retrying.
        public int RetryIntervalMilliseconds { get; set; } = 300000; // Default to 5 minutes

        public int SortOrder { get; set; } = 100;

        public bool IsDeleted { get; set; } = false;

        public bool IsInMaintenance { get; set; } = false;

        public int FailedCheckCount { get; set; } = 0;

        public int? ServiceGroupId { get; set; }
        [ForeignKey("ServiceGroupId")]
        public virtual ServiceGroup? ServiceGroup { get; set; }

        public virtual ICollection<DowntimeEvent> DowntimeHistory { get; set; } = new List<DowntimeEvent>();

        // Fields for Redis
        public string? RedisUsername { get; set; }
        public string? RedisPassword { get; set; }
        public int? RedisDbNumber { get; set; }
    }
}

