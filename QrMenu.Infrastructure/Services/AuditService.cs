using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Data;
namespace QrMenu.Infrastructure.Services;
public interface IAuditService { Task LogAsync(string userId, string userEmail, string action, string entityType, string? entityId=null, string? details=null, string? ipAddress=null); }
public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;
    public async Task LogAsync(string userId, string userEmail, string action, string entityType, string? entityId=null, string? details=null, string? ipAddress=null)
    {
        _db.AuditLogs.Add(new AuditLog { UserId=userId, UserEmail=userEmail, Action=action, EntityType=entityType, EntityId=entityId, Details=details, IpAddress=ipAddress, Timestamp=DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }
}
