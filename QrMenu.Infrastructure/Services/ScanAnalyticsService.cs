using Microsoft.EntityFrameworkCore;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Data;
namespace QrMenu.Infrastructure.Services;
public interface IScanAnalyticsService { Task RecordScanAsync(int restaurantId, string? ip, string? ua); Task<ScanStats> GetStatsAsync(int restaurantId); }
public class ScanStats { public int TotalScans{get;set;} public int ScansToday{get;set;} public int ScansThisWeek{get;set;} public int ScansThisMonth{get;set;} public List<DailyScanCount> Last7Days{get;set;}=new(); }
public class DailyScanCount { public DateTime Date{get;set;} public int Count{get;set;} }
public class ScanAnalyticsService : IScanAnalyticsService
{
    private readonly AppDbContext _db;
    public ScanAnalyticsService(AppDbContext db) => _db = db;
    public async Task RecordScanAsync(int restaurantId, string? ip, string? ua) { _db.MenuScanLogs.Add(new MenuScanLog{RestaurantId=restaurantId,IpAddress=ip,UserAgent=ua,ScannedAt=DateTime.UtcNow}); await _db.SaveChangesAsync(); }
    public async Task<ScanStats> GetStatsAsync(int restaurantId)
    {
        var now = DateTime.UtcNow; var today = now.Date;
        var all = await _db.MenuScanLogs.Where(s => s.RestaurantId==restaurantId).ToListAsync();
        return new ScanStats { TotalScans=all.Count, ScansToday=all.Count(s=>s.ScannedAt.Date==today), ScansThisWeek=all.Count(s=>s.ScannedAt.Date>=today.AddDays(-7)), ScansThisMonth=all.Count(s=>s.ScannedAt.Date>=today.AddDays(-30)),
            Last7Days=Enumerable.Range(0,7).Select(i=>today.AddDays(-i)).Select(d=>new DailyScanCount{Date=d,Count=all.Count(s=>s.ScannedAt.Date==d)}).OrderBy(d=>d.Date).ToList() };
    }
}
