using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QrMenu.Domain.Entities;
namespace QrMenu.Infrastructure.Data;
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<MenuScanLog> MenuScanLogs => Set<MenuScanLog>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Restaurant>(e => {
            e.HasIndex(r => r.Slug).IsUnique();
            e.Property(r => r.ThemeColor).HasDefaultValue("#e85d26");
            e.HasOne(r => r.Owner).WithMany(u => u.Restaurants).HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Category>(e => e.HasOne(c => c.Restaurant).WithMany(r => r.Categories).HasForeignKey(c => c.RestaurantId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<MenuItem>(e => {
            e.Property(m => m.Price).HasPrecision(10,2);
            e.HasOne(m => m.Category).WithMany(c => c.MenuItems).HasForeignKey(m => m.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<EmailVerificationCode>(e => e.HasOne(ev => ev.User).WithMany().HasForeignKey(ev => ev.UserId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<MenuScanLog>(e => { e.HasOne(s => s.Restaurant).WithMany().HasForeignKey(s => s.RestaurantId).OnDelete(DeleteBehavior.Cascade); e.HasIndex(s => s.RestaurantId); });
    }
}
