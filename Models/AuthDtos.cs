using System.ComponentModel.DataAnnotations;

namespace MonitoringApplication.Models
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }
    }

    public class RegisterDto
    {
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }

        // NEW: Field for Admin to specify the role
        [Required(ErrorMessage = "Role is required")]
        public string? Role { get; set; }
    }
}
