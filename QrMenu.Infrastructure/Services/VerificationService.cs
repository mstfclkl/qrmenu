using Microsoft.EntityFrameworkCore;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Data;
namespace QrMenu.Infrastructure.Services;
public interface IVerificationService { Task<string> GenerateAndSaveCodeAsync(string userId, string email); Task<bool> VerifyCodeAsync(string userId, string code); Task<bool> HasValidCodeAsync(string userId); Task InvalidateCodesAsync(string userId); }
public class VerificationService : IVerificationService
{
    private readonly AppDbContext _db;
    public VerificationService(AppDbContext db) => _db = db;
    public async Task<string> GenerateAndSaveCodeAsync(string userId, string email)
    {
        await InvalidateCodesAsync(userId);
        var code = Random.Shared.Next(100000, 1000000).ToString();
        _db.EmailVerificationCodes.Add(new EmailVerificationCode { UserId=userId, Email=email, Code=code, ExpiresAt=DateTime.UtcNow.AddMinutes(15) });
        await _db.SaveChangesAsync();
        return code;
    }
    public async Task<bool> VerifyCodeAsync(string userId, string code)
    {
        var r = await _db.EmailVerificationCodes.Where(v => v.UserId==userId && v.Code==code && !v.IsUsed && v.ExpiresAt>DateTime.UtcNow).FirstOrDefaultAsync();
        if (r==null) return false;
        r.IsUsed=true; await _db.SaveChangesAsync(); return true;
    }
    public async Task<bool> HasValidCodeAsync(string userId) => await _db.EmailVerificationCodes.AnyAsync(v => v.UserId==userId && !v.IsUsed && v.ExpiresAt>DateTime.UtcNow);
    public async Task InvalidateCodesAsync(string userId) { var codes = await _db.EmailVerificationCodes.Where(v => v.UserId==userId && !v.IsUsed).ToListAsync(); codes.ForEach(c => c.IsUsed=true); await _db.SaveChangesAsync(); }
}
