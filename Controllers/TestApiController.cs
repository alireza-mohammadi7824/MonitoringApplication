using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MonitoringApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous] // این کنترلر برای تست نیازی به لاگین ندارد
    public class TestApiController : ControllerBase
    {
        private static readonly Random _random = new Random();
        private static int _flakyCounter = 0;

        /// <summary>
        /// این Endpoint به صورت تصادفی وضعیت موفق یا خطا برمی‌گرداند.
        /// شانس موفقیت آن 60% است.
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            if (_random.NextDouble() < 0.6)
            {
                return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
            }
            else
            {
                return StatusCode(500, new { Status = "Unhealthy - Simulated Error", Timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// NEW: این Endpoint برای تست تاریخچه قطعی طراحی شده است.
        /// 5 بار اول که فراخوانی شود، خطا برمی‌گرداند و پس از آن همیشه موفق خواهد بود.
        /// </summary>
        [HttpGet("flaky-status")]
        public IActionResult GetFlakyStatus()
        {
            _flakyCounter++;

            if (_flakyCounter <= 5)
            {
                return StatusCode(503, new { Status = "Unhealthy - Service Warming Up", Timestamp = DateTime.UtcNow, CallCount = _flakyCounter });
            }

            return Ok(new { Status = "Healthy and Stable", Timestamp = DateTime.UtcNow, CallCount = _flakyCounter });
        }
    }
}

