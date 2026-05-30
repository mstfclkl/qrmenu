using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Interfaces;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Services;

namespace QrMenu.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly IRestaurantService _restaurantService;
    private readonly IUserService _userService;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public AdminController(IRestaurantService rs, IUserService us, IAuditService audit,
        UserManager<ApplicationUser> um, IEmailService email)
    { _restaurantService=rs; _userService=us; _audit=audit; _userManager=um; _emailService=email; }

    [HttpGet("")]
    public async Task<IActionResult> Dashboard()
    {
        var restaurants = await _restaurantService.GetAllAsync();
        var owners = await _userService.GetAllOwnersAsync();
        ViewBag.Restaurants = restaurants;
        ViewBag.Owners = owners;
        ViewBag.PendingCount = restaurants.Count(r => !r.IsApproved);
        ViewBag.ActiveCount = restaurants.Count(r => r.IsActive && r.IsApproved);
        ViewBag.TotalOwners = owners.Count;
        return View();
    }

    [HttpGet("restaurants")]
    public async Task<IActionResult> Restaurants() => View(await _restaurantService.GetAllAsync());

    [HttpPost("restaurants/{id}/approve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        await _restaurantService.ApproveAsync(id);
        var admin = await _userManager.GetUserAsync(User);
        if (admin != null)
            await _audit.LogAsync(admin.Id, admin.Email!, "ApproveRestaurant", "Restaurant", id.ToString(), null, HttpContext.Connection.RemoteIpAddress?.ToString());
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant != null)
        {
            var owner = await _userManager.FindByIdAsync(restaurant.OwnerId);
            if (owner != null)
            {
                var menuUrl = $"{Request.Scheme}://{Request.Host}/menu/{restaurant.Slug}";
                try { await _emailService.SendRestaurantApprovedAsync(owner.Email!, owner.FirstName, restaurant.Name, menuUrl); } catch { }
            }
        }
        TempData["Success"] = "Restaurant approved and owner notified.";
        return RedirectToAction(nameof(Restaurants));
    }

    [HttpPost("restaurants/{id}/reject"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var admin = await _userManager.GetUserAsync(User);
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant != null)
        {
            var owner = await _userManager.FindByIdAsync(restaurant.OwnerId);
            if (owner != null)
                try { await _emailService.SendRestaurantRejectedAsync(owner.Email!, owner.FirstName, restaurant.Name, reason ?? "No reason provided."); } catch { }
        }
        if (admin != null)
            await _audit.LogAsync(admin.Id, admin.Email!, "RejectRestaurant", "Restaurant", id.ToString(), reason, HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = "Restaurant rejected and owner notified.";
        return RedirectToAction(nameof(Restaurants));
    }

    [HttpPost("restaurants/{id}/toggle"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        await _restaurantService.ToggleActiveAsync(id);
        TempData["Success"] = "Restaurant status updated.";
        return RedirectToAction(nameof(Restaurants));
    }

    [HttpPost("restaurants/{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _restaurantService.DeleteAsync(id);
        var admin = await _userManager.GetUserAsync(User);
        if (admin != null)
            await _audit.LogAsync(admin.Id, admin.Email!, "DeleteRestaurant", "Restaurant", id.ToString(), null, HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = "Restaurant deleted.";
        return RedirectToAction(nameof(Restaurants));
    }

    [HttpGet("owners")]
    public async Task<IActionResult> Owners() => View(await _userService.GetAllOwnersAsync());

    [HttpGet("owners/create")]
    public IActionResult CreateOwner() => View();

    [HttpPost("owners/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOwner(string firstName, string lastName, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { TempData["Error"] = "All fields are required."; return View(); }
        if (await _userManager.FindByEmailAsync(email) != null)
        { TempData["Error"] = "Email already exists."; return View(); }
        var user = new ApplicationUser { UserName=email.Trim(), Email=email.Trim(), FirstName=firstName.Trim(), LastName=(lastName??string.Empty).Trim(), EmailConfirmed=true, IsActive=true };
        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Owner");
            var admin = await _userManager.GetUserAsync(User);
            if (admin != null)
                await _audit.LogAsync(admin.Id, admin.Email!, "AdminCreatedOwner", "User", user.Id, email, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = $"Owner account created for {email}.";
            return RedirectToAction(nameof(Owners));
        }
        TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        return View();
    }

    [HttpPost("owners/{id}/deactivate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateOwner(string id)
    {
        await _userService.DeactivateAsync(id);
        TempData["Success"] = "Owner deactivated.";
        return RedirectToAction(nameof(Owners));
    }

    [HttpPost("owners/{id}/activate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateOwner(string id)
    {
        await _userService.ActivateAsync(id);
        TempData["Success"] = "Owner activated.";
        return RedirectToAction(nameof(Owners));
    }

    [HttpGet("audit")]
    public IActionResult AuditLog()
    {
        var db = HttpContext.RequestServices.GetRequiredService<QrMenu.Infrastructure.Data.AppDbContext>();
        var logs = db.AuditLogs.OrderByDescending(l => l.Timestamp).Take(200).ToList();
        return View(logs);
    }
}
