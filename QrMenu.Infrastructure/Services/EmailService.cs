using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace QrMenu.Infrastructure.Services;
public interface IEmailService
{
    Task SendVerificationCodeAsync(string toEmail, string firstName, string code);
    Task SendWelcomeEmailAsync(string toEmail, string firstName);
    Task SendPasswordResetAsync(string toEmail, string firstName, string resetLink);
    Task SendRestaurantApprovedAsync(string toEmail, string firstName, string restaurantName, string menuUrl);
    Task SendRestaurantRejectedAsync(string toEmail, string firstName, string restaurantName, string reason);
    Task SendAccountDeletedAsync(string toEmail, string firstName);
}
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;
    public SmtpEmailService(IConfiguration c, ILogger<SmtpEmailService> l) { _config=c; _logger=l; }
    public async Task SendVerificationCodeAsync(string to, string name, string code) => await SendAsync(to, "Verify your QRMenu account", $@"<div style='font-family:Arial;max-width:520px;margin:40px auto;background:white;border-radius:12px;overflow:hidden'><div style='background:#1a1714;padding:32px;text-align:center'><span style='color:#e85d26;font-size:28px;font-weight:bold'>QRMenu</span></div><div style='padding:40px'><h2 style='color:#1a1714'>Hi {name},</h2><p style='color:#4a4540'>Enter this code to verify your email:</p><div style='background:#faf9f7;border:2px solid #e8e4de;border-radius:10px;padding:28px;text-align:center;margin:20px 0'><div style='font-size:44px;font-weight:bold;letter-spacing:14px;color:#c8420a;font-family:monospace'>{code}</div><div style='color:#8a8480;font-size:13px;margin-top:10px'>Expires in 15 minutes</div></div><p style='color:#8a8480;font-size:13px'>If you did not create a QRMenu account, ignore this email.</p></div></div>");
    public async Task SendWelcomeEmailAsync(string to, string name) => await SendAsync(to, "Welcome to QRMenu!", $@"<div style='font-family:Arial;max-width:520px;margin:40px auto;background:white;border-radius:12px;overflow:hidden'><div style='background:#1a1714;padding:32px;text-align:center'><span style='color:#e85d26;font-size:28px;font-weight:bold'>QRMenu</span></div><div style='padding:40px'><h2 style='color:#1a1714'>Welcome, {name}!</h2><p style='color:#4a4540'>Your email is verified. Create your restaurant and start building your menu.</p></div></div>");
    public async Task SendPasswordResetAsync(string to, string name, string link) => await SendAsync(to, "Reset your QRMenu password", $@"<div style='font-family:Arial;max-width:520px;margin:40px auto;background:white;border-radius:12px;overflow:hidden'><div style='background:#1a1714;padding:32px;text-align:center'><span style='color:#e85d26;font-size:28px;font-weight:bold'>QRMenu</span></div><div style='padding:40px'><h2 style='color:#1a1714'>Hi {name},</h2><p style='color:#4a4540'>Click the button below to reset your password. This link expires in 1 hour.</p><div style='text-align:center;margin:28px 0'><a href='{link}' style='display:inline-block;background:#c8420a;color:white;padding:14px 32px;border-radius:8px;font-weight:bold;text-decoration:none'>Reset password</a></div><p style='color:#8a8480;font-size:13px'>If you did not request this, ignore this email.</p></div></div>");
    public async Task SendRestaurantApprovedAsync(string to, string name, string restaurant, string menuUrl) => await SendAsync(to, $"Your restaurant {restaurant} is now live!", $@"<div style='font-family:Arial;max-width:520px;margin:40px auto;background:white;border-radius:12px;overflow:hidden'><div style='background:#1a1714;padding:32px;text-align:center'><span style='color:#e85d26;font-size:28px;font-weight:bold'>QRMenu</span></div><div style='padding:40px'><h2 style='color:#1a1714'>Great news, {name}!</h2><p style='color:#4a4540'>Your restaurant <strong>{restaurant}</strong> has been approved and is now live.</p><div style='text-align:center;margin:28px 0'><a href='{menuUrl}' style='display:inline-block;background:#c8420a;color:white;padding:14px 32px;border-radius:8px;font-weight:bold;text-decoration:none'>View live menu</a></div></div></div>");
    public async Task SendRestaurantRejectedAsync(string to, string name, string restaurant, string reason) => await SendAsync(to, $"Update on your restaurant {restaurant}", $@"<div style='font-family:Arial;max-width:520px;margin:40px auto;background:white;border-radius:12px;overflow:hidden'><div style='background:#1a1714;padding:32px;text-align:center'><span style='color:#e85d26;font-size:28px;font-weight:bold'>QRMenu</span></div><div style='padding:40px'><h2 style='color:#1a1714'>Hi {name},</h2><p style='color:#4a4540'>Your restaurant <strong>{restaurant}</strong> could not be approved.</p><div style='background:#fde8e6;border-left:4px solid #b03a2e;padding:14px;border-radius:4px;margin:16px 0'><p style='color:#7a1f1a;margin:0'><strong>Reason:</strong> {reason}</p></div><p style='color:#4a4540'>Please update your details and resubmit.</p></div></div>");
    public async Task SendAccountDeletedAsync(string to, string name) => await SendAsync(to, "Your QRMenu account has been deleted", $@"<div style='font-family:Arial;max-width:520px;margin:40px auto;background:white;border-radius:12px;overflow:hidden'><div style='background:#1a1714;padding:32px;text-align:center'><span style='color:#e85d26;font-size:28px;font-weight:bold'>QRMenu</span></div><div style='padding:40px'><h2 style='color:#1a1714'>Hi {name},</h2><p style='color:#4a4540'>Your QRMenu account and all associated data has been permanently deleted.</p></div></div>");
    private async Task SendAsync(string to, string subject, string html)
    {
        var s = _config.GetSection("Smtp");
        var user = s["Username"] ?? ""; var pass = s["Password"] ?? "";
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) { _logger.LogWarning("SMTP not configured - skipping email to {Email}", to); return; }
        try
        {
            using var client = new SmtpClient(s["Host"] ?? "smtp.gmail.com", int.Parse(s["Port"] ?? "587")) { Credentials=new NetworkCredential(user,pass), EnableSsl=true };
            using var msg = new MailMessage { From=new MailAddress(s["FromEmail"]??user, s["FromName"]??"QRMenu"), Subject=subject, Body=html, IsBodyHtml=true };
            msg.To.Add(to);
            await client.SendMailAsync(msg);
            _logger.LogInformation("Email sent to {Email}", to);
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to send email to {Email}", to); throw; }
    }
}
