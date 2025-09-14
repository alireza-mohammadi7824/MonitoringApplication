using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitoringApplication.Data;
using MonitoringApplication.Models;

namespace MonitoringApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ServiceGroupsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ServiceGroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceGroup>>> GetServiceGroups()
        {
            return await _context.ServiceGroups.OrderBy(g => g.Name).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ServiceGroup>> PostServiceGroup(ServiceGroup serviceGroup)
        {
            _context.ServiceGroups.Add(serviceGroup);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetServiceGroups), new { id = serviceGroup.Id }, serviceGroup);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServiceGroup(int id)
        {
            var serviceGroup = await _context.ServiceGroups.FindAsync(id);
            if (serviceGroup == null)
            {
                return NotFound();
            }

            // بهبود: اعتبارسنجی سمت سرور برای جلوگیری از حذف گروهی که سرویس دارد
            var isGroupInUse = await _context.Services.AnyAsync(s => s.ServiceGroupId == id);
            if (isGroupInUse)
            {
                return BadRequest("نمی‌توانید گروهی که سرویس به آن اختصاص دارد را حذف کنید.");
            }

            _context.ServiceGroups.Remove(serviceGroup);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}

