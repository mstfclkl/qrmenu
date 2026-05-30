using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QrMenu.Application.DTOs;
using QrMenu.Application.Interfaces;
using QrMenu.Infrastructure.Services;
namespace QrMenu.Web.Controllers;
public class MenuController : Controller
{
    private readonly IPublicMenuService _menuService;
    private readonly IScanAnalyticsService _analytics;
    private readonly IMemoryCache _cache;
    public MenuController(IPublicMenuService ms, IScanAnalyticsService a, IMemoryCache c) { _menuService=ms; _analytics=a; _cache=c; }

    [Route("menu/{slug}")]
    public async Task<IActionResult> Index(string slug)
    {
        var cacheKey = $"menu:{slug}";
        if (!_cache.TryGetValue(cacheKey, out PublicMenuDto? menu))
        {
            menu = await _menuService.GetPublicMenuAsync(slug);
            if (menu == null) return NotFound();
            _cache.Set(cacheKey, menu, TimeSpan.FromSeconds(60));
        }
        if (menu == null) return NotFound();
        _ = Task.Run(async () => {
            try { await _analytics.RecordScanAsync(menu.Restaurant.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()); }
            catch { }
        });
        return View(menu);
    }
}
