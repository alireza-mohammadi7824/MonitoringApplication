using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitoringApplication.Models
{
    /// <summary>
    /// رکوردی برای ثبت یک دوره قطعی سرویس.
    /// </summary>
    public class DowntimeEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string MonitoredServiceId { get; set; }

        [ForeignKey("MonitoredServiceId")]
        public virtual MonitoredService MonitoredService { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }
    }
}
