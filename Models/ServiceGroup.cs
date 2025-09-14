using System.ComponentModel.DataAnnotations;

namespace MonitoringApplication.Models
{
    public class ServiceGroup
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "نام گروه الزامی است")]
        public string Name { get; set; } = string.Empty;

        public ICollection<MonitoredService> Services { get; set; } = new List<MonitoredService>();
    }
}
