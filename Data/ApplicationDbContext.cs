using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MonitoringApplication.Models;

namespace MonitoringApplication.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<MonitoredService> Services { get; set; }
        public DbSet<DowntimeEvent> DowntimeEvents { get; set; }

        // --- NEW: Add DbSet for ServiceGroups ---
        public DbSet<ServiceGroup> ServiceGroups { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<MonitoredService>()
                .HasQueryFilter(s => !s.IsDeleted);
        }
    }
}

