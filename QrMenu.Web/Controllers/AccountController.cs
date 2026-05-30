using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Services;

namespace QrMenu.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _audit;
    private readonly IEmailService _emailService;
    private readonly IVerificationService _verificationService;
    private readonly IConfiguration _config;

    public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm,
        IAuditService audit, IEmailService email, IVerificationService verify, IConfiguration config)
    {
        _userManager = um; _signInManager = sm; _audit = audit;
        _emailService = email; _verificationService = verify; _config = config;
    }

    private bool SmtpConfigured =>
        !string.IsNullOrEmpty(_config["Smtp:Username"]) && !string.IsNullOrEmpty(_config["Smtp:Password"]);

    private void SetSession(string userId, string email, string firstName)
    {
        HttpContext.Session.SetString("VerifyUserId", userId);
        HttpContext.Session.SetString("VerifyEmail", email);
        HttpContext.Session.SetString("VerifyFirstName", firstName);
    }

    private (string? userId, string? email, string? firstName) GetSession() =>
    (
        HttpContext.Session.GetString("VerifyUserId") is { Length: > 0 } u ? u : null,
        HttpContext.Session.GetString("VerifyEmail") is { Length: > 0 } e ? e : null,
        HttpContext.Session.GetString("VerifyFirstName") is { Length: > 0 } f ? f : null
    );

    private void ClearSession()
    {
        HttpContext.Session.SetString("VerifyUserId", "");
        HttpContext.Session.SetString("VerifyEmail", "");
        HttpContext.Session.SetString("VerifyFirstName", "");
    }

    // ── Login ─────────────────────────────────────────────────────
    [HttpGet] public IActionResult Login(string? returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { ModelState.AddModelError("", "Email and password are required."); return View(); }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null || !user.IsActive) { await Task.Delay(300); ModelState.AddModelError("", "Invalid email or password."); return View(); }

        if (!user.EmailConfirmed)
        { SetSession(user.Id, user.Email!, user.FirstName); TempData["Error"] = "Please verify your email first."; return RedirectToAction(nameof(VerifyEmail)); }

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await _audit.LogAsync(user.Id, user.Email!, "Login", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (await _userManager.IsInRoleAsync(user, "Admin")) return RedirectToAction("Dashboard", "Admin");
            if (await _userManager.IsInRoleAsync(user, "Owner")) return RedirectToAction("Index", "Owner");
            return LocalRedirect(returnUrl ?? "/");
        }
        if (result.IsLockedOut) { ModelState.AddModelError("", "Account locked for 15 minutes after too many failed attempts."); return View(); }
        await _audit.LogAsync(user.Id, user.Email!, "LoginFailed", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
        ModelState.AddModelError("", "Invalid email or password.");
        return View();
    }

    // ── Register ──────────────────────────────────────────────────
    [HttpGet] public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register(string firstName, string lastName, string email, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { ModelState.AddModelError("", "All fields are required."); return View(); }

        if (password != confirmPassword) { ModelState.AddModelError("", "Passwords do not match."); return View(); }

        if (await _userManager.FindByEmailAsync(email.Trim()) != null)
        { ModelState.AddModelError("", "An account with this email already exists."); return View(); }

        var user = new ApplicationUser { UserName=email.Trim(), Email=email.Trim(), FirstName=firstName.Trim(), LastName=lastName.Trim(), EmailConfirmed=false };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded) { foreach (var e in result.Errors) ModelState.AddModelError("", e.Description); return View(); }

        await _userManager.AddToRoleAsync(user, "Owner");
        var code = await _verificationService.GenerateAndSaveCodeAsync(user.Id, user.Email!);

        bool emailSent = false;
        if (SmtpConfigured)
        {
            try { await _emailService.SendVerificationCodeAsync(user.Email!, user.FirstName, code); emailSent = true; }
            catch (Exception ex) { await _userManager.DeleteAsync(user); ModelState.AddModelError("", $"Could not send verification email: {ex.Message}"); return View(); }
        }

        await _audit.LogAsync(user.Id, user.Email!, "Register", "User", user.Id, $"Email {(emailSent ? "sent" : "shown on screen")}", HttpContext.Connection.RemoteIpAddress?.ToString());
        SetSession(user.Id, user.Email!, user.FirstName);
        if (!emailSent) TempData["DevCode"] = code;
        return RedirectToAction(nameof(VerifyEmail));
    }

    // ── Verify Email ──────────────────────────────────────────────
    [HttpGet]
    public IActionResult VerifyEmail()
    {
        var (userId, email, _) = GetSession();
        if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));
        ViewBag.Email = email;
        ViewBag.SmtpConfigured = SmtpConfigured;
        ViewBag.DevCode = TempData["DevCode"];
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> VerifyEmail(string code)
    {
        var (userId, email, _) = GetSession();
        if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));
        ViewBag.Email = email; ViewBag.SmtpConfigured = SmtpConfigured;

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6)
        { ModelState.AddModelError("", "Please enter the 6-digit code."); return View(); }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToAction(nameof(Login));

        if (!await _verificationService.VerifyCodeAsync(userId, code.Trim()))
        { ModelState.AddModelError("", "Invalid or expired code. Request a new one below."); return View(); }

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        await _signInManager.SignInAsync(user, isPersistent: false);
        ClearSession();
        if (SmtpConfigured) try { await _emailService.SendWelcomeEmailAsync(user.Email!, user.FirstName); } catch { }
        await _audit.LogAsync(user.Id, user.Email!, "EmailVerified", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = $"Welcome, {user.FirstName}! Your email is verified.";
        return RedirectToAction("Index", "Owner");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> ResendCode()
    {
        var (userId, email, firstName) = GetSession();
        if (string.IsNullOrEmpty(userId)) { TempData["Error"] = "Session expired. Please log in again."; return RedirectToAction(nameof(Login)); }
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) { TempData["Error"] = "Account not found."; return RedirectToAction(nameof(Register)); }
        if (user.EmailConfirmed) { TempData["Success"] = "Email already verified. Please log in."; return RedirectToAction(nameof(Login)); }

        var code = await _verificationService.GenerateAndSaveCodeAsync(userId, email!);
        bool sent = false;
        if (SmtpConfigured) try { await _emailService.SendVerificationCodeAsync(email!, firstName ?? "", code); sent = true; } catch { }

        SetSession(userId, email!, firstName ?? "");
        if (!sent) TempData["DevCode"] = code;
        TempData["Success"] = sent ? "New code sent to your email." : "New code generated.";
        return RedirectToAction(nameof(VerifyEmail));
    }

    // ── Forgot / Reset Password ───────────────────────────────────
    [HttpGet] public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) { ModelState.AddModelError("", "Please enter your email."); return View(); }
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user != null && user.IsActive && user.EmailConfirmed)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?userId={user.Id}&token={Uri.EscapeDataString(token)}";
            if (SmtpConfigured) try { await _emailService.SendPasswordResetAsync(user.Email!, user.FirstName, link); } catch { }
            await _audit.LogAsync(user.Id, user.Email!, "PasswordResetRequested", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        TempData["Success"] = "If that email exists, a reset link has been sent.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet] public IActionResult ResetPassword(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return RedirectToAction(nameof(Login));
        ViewBag.UserId = userId; ViewBag.Token = token; return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(string userId, string token, string newPassword, string confirmPassword)
    {
        ViewBag.UserId = userId; ViewBag.Token = token;
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
        { ModelState.AddModelError("", "Passwords do not match or are empty."); return View(); }
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToAction(nameof(Login));
        var result = await _userManager.ResetPasswordAsync(user, Uri.UnescapeDataString(token), newPassword);
        if (result.Succeeded)
        {
            await _audit.LogAsync(user.Id, user.Email!, "PasswordReset", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = "Password reset successfully. Please log in.";
            return RedirectToAction(nameof(Login));
        }
        foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
        return View();
    }

    // ── Delete Account ────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> DeleteAccount(string confirmText)
    {
        if (confirmText?.ToUpper() != "DELETE") { TempData["Error"] = "Type DELETE to confirm."; return RedirectToAction("Index", "Owner"); }
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));
        var email = user.Email!; var name = user.FirstName;
        await _signInManager.SignOutAsync(); ClearSession();
        await _audit.LogAsync(user.Id, email, "AccountDeleted", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
        await _userManager.DeleteAsync(user);
        if (SmtpConfigured) try { await _emailService.SendAccountDeletedAsync(email, name); } catch { }
        TempData["Success"] = "Your account has been permanently deleted.";
        return RedirectToAction("Index", "Home");
    }

    // ── Logout ────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) await _audit.LogAsync(user.Id, user.Email!, "Logout", "User", user.Id, null, HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        ClearSession();
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
