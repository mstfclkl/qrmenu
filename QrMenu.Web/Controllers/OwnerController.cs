using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.DTOs;
using QrMenu.Application.Interfaces;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Services;
using QrMenu.Web.Models;
using QrMenu.Web.Services;
using QRCoder;

namespace QrMenu.Web.Controllers;

[Authorize(Roles = "Owner")]
[Route("dashboard")]
public class OwnerController : Controller
{
    private readonly IRestaurantService _restaurantService;
    private readonly ICategoryService _categoryService;
    private readonly IMenuItemService _menuItemService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditService _audit;
    private readonly IScanAnalyticsService _analytics;

    public OwnerController(IRestaurantService rs, ICategoryService cs, IMenuItemService ms,
        UserManager<ApplicationUser> um, IWebHostEnvironment env, IAuditService audit, IScanAnalyticsService analytics)
    { _restaurantService=rs; _categoryService=cs; _menuItemService=ms; _userManager=um; _env=env; _audit=audit; _analytics=analytics; }

    private async Task<string?> GetOwnerId() => (await _userManager.GetUserAsync(User))?.Id;
    private IActionResult UserNotFound() => RedirectToAction("Login", "Account");

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        return View(await _restaurantService.GetByOwnerAsync(ownerId));
    }

    // ── Stats ─────────────────────────────────────────────────────
    [HttpGet("restaurant/{id}/stats")]
    public async Task<IActionResult> Stats(int id)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        var stats = await _analytics.GetStatsAsync(id);
        var categories = await _categoryService.GetByRestaurantAsync(id, includeItems: true);
        ViewBag.Restaurant = restaurant;
        ViewBag.Stats = stats;
        ViewBag.SoldOutItems = categories.SelectMany(c => c.Items).Where(i => i.IsSoldOut).ToList();
        ViewBag.TotalItems = categories.Sum(c => c.ItemCount);
        return View();
    }

    // ── Restaurant ────────────────────────────────────────────────
    [HttpGet("restaurant/create")]
    public IActionResult CreateRestaurant() => View(new CreateRestaurantDto());

    [HttpPost("restaurant/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRestaurant(CreateRestaurantDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        dto.OpeningHoursJson = BuildOpeningHoursJson(Request.Form);
        dto.CurrencySymbol = GetCurrencySymbol(dto.CurrencyCode);
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.CreateAsync(dto, ownerId);
        TempData["Success"] = "Restaurant created! Awaiting admin approval.";
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurant.Id });
    }

    [HttpGet("restaurant/{id}")]
    public async Task<IActionResult> ManageRestaurant(int id)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        var categories = await _categoryService.GetByRestaurantAsync(id, includeItems: true);
        ViewBag.Restaurant = restaurant;
        ViewBag.Categories = categories;
        return View(restaurant);
    }

    [HttpPost("restaurant/{id}/update"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRestaurant(int id, UpdateRestaurantDto dto)
    {
        dto.Id = id;
        dto.OpeningHoursJson = BuildOpeningHoursJson(Request.Form);
        if (string.IsNullOrEmpty(dto.CurrencySymbol)) dto.CurrencySymbol = GetCurrencySymbol(dto.CurrencyCode);
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        await _restaurantService.UpdateAsync(dto);
        TempData["Success"] = "Restaurant updated.";
        return RedirectToAction(nameof(ManageRestaurant), new { id });
    }

    [HttpPost("restaurant/{id}/upload-logo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(int id, IFormFile? file)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        if (file == null || file.Length == 0) { TempData["Error"] = "Please select an image file."; return RedirectToAction(nameof(ManageRestaurant), new { id }); }
        var (valid, error) = FileValidator.Validate(file);
        if (!valid) { TempData["Error"] = error; return RedirectToAction(nameof(ManageRestaurant), new { id }); }
        try
        {
            var (logoDir, logoUrl) = GetUploadPath("logos");
            Directory.CreateDirectory(logoDir);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"{Guid.NewGuid():N}{ext}";
            using (var s = new FileStream(Path.Combine(logoDir, fileName), FileMode.Create)) await file.CopyToAsync(s);
            await _restaurantService.UpdateLogoAsync(id, $"{logoUrl}/{fileName}");
            var user = await _userManager.GetUserAsync(User);
            if (user != null) await _audit.LogAsync(user.Id, user.Email!, "UploadLogo", "Restaurant", id.ToString(), fileName, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = "Logo uploaded.";
        }
        catch (Exception ex) { TempData["Error"] = $"Upload failed: {ex.Message}"; }
        return RedirectToAction(nameof(ManageRestaurant), new { id });
    }

    [HttpGet("restaurant/{id}/qr")]
    public async Task<IActionResult> GenerateQr(int id)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(id);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/menu/{restaurant.Slug}";
        try
        {
            var dir = Directory.Exists("/data") ? "/data/qr" : Path.Combine(
                string.IsNullOrEmpty(_env.WebRootPath) ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot") : _env.WebRootPath, "qr");
            Directory.CreateDirectory(dir);
            using var qrGen = new QRCodeGenerator();
            using var qrData = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            var bytes = qrCode.GetGraphic(20);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, $"{restaurant.Slug}.png"), bytes);
            await _restaurantService.GenerateQrCodeAsync(id, baseUrl);
            TempData["Success"] = "QR code generated.";
        }
        catch (Exception ex) { TempData["Error"] = $"QR generation failed: {ex.Message}"; }
        return RedirectToAction(nameof(ManageRestaurant), new { id });
    }

    // ── Categories ────────────────────────────────────────────────
    [HttpPost("restaurant/{restaurantId}/category/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(int restaurantId, string Name)
    {
        if (string.IsNullOrWhiteSpace(Name)) { TempData["Error"] = "Category name cannot be empty."; return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId }); }
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(restaurantId);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        try
        {
            await _categoryService.CreateAsync(new CreateCategoryDto { Name=Name.Trim(), RestaurantId=restaurantId, DisplayOrder=0 });
            TempData["Success"] = "Category added.";
        }
        catch (Exception ex) { TempData["Error"] = $"Failed to add category: {ex.Message}"; }
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    [HttpPost("restaurant/{restaurantId}/category/reorder"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCategories(int restaurantId, [FromBody] ReorderRequest req)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return Forbid();
        var restaurant = await _restaurantService.GetByIdAsync(restaurantId);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        await _categoryService.ReorderAsync(restaurantId, req.OrderedIds);
        return Ok();
    }

    [HttpPost("category/{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, int restaurantId)
    {
        await _categoryService.DeleteAsync(id);
        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    // ── Menu Items ────────────────────────────────────────────────
    [HttpGet("item/create")]
    public async Task<IActionResult> CreateItem(int categoryId, int restaurantId)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(restaurantId);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();
        ViewBag.RestaurantId = restaurantId;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await _categoryService.GetByRestaurantAsync(restaurantId);
        return View(new CreateMenuItemDto { CategoryId=categoryId, IsAvailable=true });
    }

    [HttpPost("item/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(CreateMenuItemDto dto, int restaurantId)
    {
        var ownerId = await GetOwnerId();
        if (ownerId == null) return UserNotFound();
        var restaurant = await _restaurantService.GetByIdAsync(restaurantId);
        if (restaurant == null || restaurant.OwnerId != ownerId) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Name)) ModelState.AddModelError(nameof(dto.Name), "Item name is required.");
        if (dto.Price < 0) ModelState.AddModelError(nameof(dto.Price), "Price cannot be negative.");
        if (dto.CategoryId <= 0) ModelState.AddModelError(nameof(dto.CategoryId), "Please select a category.");

        if (!ModelState.IsValid)
        {
            ViewBag.RestaurantId = restaurantId;
            ViewBag.CategoryId = dto.CategoryId;
            ViewBag.Categories = await _categoryService.GetByRestaurantAsync(restaurantId);
            return View(dto);
        }

        dto.Name = Sanitizer.Clean(dto.Name) ?? dto.Name;
        dto.NameEn = Sanitizer.Clean(dto.NameEn);
        dto.Description = Sanitizer.Clean(dto.Description);
        dto.DescriptionEn = Sanitizer.Clean(dto.DescriptionEn);
        dto.Tags = Sanitizer.CleanTags(dto.Tags);
        dto.Allergens = Sanitizer.CleanTags(dto.Allergens);

        try { await _menuItemService.CreateAsync(dto); TempData["Success"] = "Menu item added."; }
        catch (Exception ex) { TempData["Error"] = $"Failed to save item: {ex.Message}"; }
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    [HttpGet("item/edit/{id}/{restaurantId}")]
    public async Task<IActionResult> EditItem(int id, int restaurantId)
    {
        var item = await _menuItemService.GetByIdAsync(id);
        if (item == null) return NotFound();
        ViewBag.RestaurantId = restaurantId;
        ViewBag.Categories = await _categoryService.GetByRestaurantAsync(restaurantId);
        return View(new CreateMenuItemDto
        {
            Name=item.Name, NameEn=item.NameEn, Description=item.Description, DescriptionEn=item.DescriptionEn,
            Price=item.Price, IsAvailable=item.IsAvailable, IsSoldOut=item.IsSoldOut, IsFeatured=item.IsFeatured,
            IsDailySpecial=item.IsDailySpecial, Allergens=item.Allergens, Tags=item.Tags,
            CategoryId=item.CategoryId, DisplayOrder=item.DisplayOrder
        });
    }

    [HttpPost("item/edit/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditItem(int id, CreateMenuItemDto dto, int restaurantId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) ModelState.AddModelError(nameof(dto.Name), "Item name is required.");
        if (dto.Price < 0) ModelState.AddModelError(nameof(dto.Price), "Price cannot be negative.");
        if (!ModelState.IsValid)
        {
            ViewBag.RestaurantId = restaurantId;
            ViewBag.Categories = await _categoryService.GetByRestaurantAsync(restaurantId);
            return View(dto);
        }
        dto.Name = Sanitizer.Clean(dto.Name) ?? dto.Name;
        dto.NameEn = Sanitizer.Clean(dto.NameEn);
        dto.Description = Sanitizer.Clean(dto.Description);
        dto.DescriptionEn = Sanitizer.Clean(dto.DescriptionEn);
        dto.Tags = Sanitizer.CleanTags(dto.Tags);
        dto.Allergens = Sanitizer.CleanTags(dto.Allergens);
        try { await _menuItemService.UpdateAsync(id, dto); TempData["Success"] = "Menu item updated."; }
        catch (Exception ex) { TempData["Error"] = $"Failed to update item: {ex.Message}"; }
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    // ── Item toggles — NOTE: id is a ROUTE parameter, no hidden input needed ──
    [HttpPost("item/{id}/toggle"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleItem(int id, int restaurantId)
    {
        await _menuItemService.ToggleAvailabilityAsync(id);
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    [HttpPost("item/{id}/soldout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSoldOut(int id, int restaurantId)
    {
        await _menuItemService.ToggleSoldOutAsync(id);
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    [HttpPost("item/{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, int restaurantId)
    {
        await _menuItemService.DeleteAsync(id);
        TempData["Success"] = "Item deleted.";
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    [HttpPost("item/{id}/upload-image"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadItemImage(int id, IFormFile? file, int restaurantId)
    {
        if (file == null || file.Length == 0) { TempData["Error"] = "Please select an image file."; return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId }); }
        var (valid, error) = FileValidator.Validate(file);
        if (!valid) { TempData["Error"] = error; return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId }); }
        try
        {
            var (itemDir, itemUrl) = GetUploadPath("items");
            Directory.CreateDirectory(itemDir);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"{Guid.NewGuid():N}{ext}";
            using (var s = new FileStream(Path.Combine(itemDir, fileName), FileMode.Create)) await file.CopyToAsync(s);
            await _menuItemService.UpdateImageAsync(id, $"{itemUrl}/{fileName}");
            TempData["Success"] = "Image uploaded successfully.";
        }
        catch (Exception ex) { TempData["Error"] = $"Upload failed: {ex.Message}"; }
        return RedirectToAction(nameof(ManageRestaurant), new { id = restaurantId });
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static string GetCurrencySymbol(string code) => code switch
    { "USD"=>"$","EUR"=>"€","GBP"=>"£","SAR"=>"﷼","AED"=>"د.إ",_=>"₺" };

    private static string? BuildOpeningHoursJson(IFormCollection form)
    {
        var days = new[]{"Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"};
        var dict = new Dictionary<string,string>();
        foreach (var day in days)
        {
            if (form[$"hours_{day}_closed"].FirstOrDefault() == "true") { dict[day]="Closed"; continue; }
            var open = form[$"hours_{day}_open"].FirstOrDefault()?.Trim();
            var close = form[$"hours_{day}_close"].FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(open) && string.IsNullOrEmpty(close)) continue;
            if (!IsValidTime(open) || !IsValidTime(close)) continue;
            dict[day] = $"{open}-{close}";
        }
        return dict.Any() ? JsonSerializer.Serialize(dict) : null;
    }

    private static bool IsValidTime(string? t)
    {
        if (string.IsNullOrEmpty(t)) return false;
        if (!TimeSpan.TryParse(t, out var ts)) return false;
        return ts.TotalHours < 24 && ts.Seconds == 0 && ts.Days == 0;
    }

    // Returns true when running on Railway (persistent volume at /data)
    // Returns (physicalDir, urlPrefix) for uploads
    // Railway: saves to /data/uploads (persistent volume), served at /data-files
    // Local:   saves to wwwroot/uploads, served at /uploads
    private (string dir, string url) GetUploadPath(string subfolder)
    {
        if (Directory.Exists("/data"))
            return (Path.Combine("/data", "uploads", subfolder), $"/data-files/{subfolder}");
        var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _env.WebRootPath;
        return (Path.Combine(webRoot, "uploads", subfolder), $"/uploads/{subfolder}");
    }
}
