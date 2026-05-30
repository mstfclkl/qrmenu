using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Interfaces;
namespace QrMenu.Web.Controllers;
public class HomeController : Controller
{
    private readonly IRestaurantService _rs;
    public HomeController(IRestaurantService rs) => _rs = rs;
    public async Task<IActionResult> Index()
    {
        var all = await _rs.GetAllAsync();
        return View(all.Where(r => r.IsActive && r.IsApproved).ToList());
    }
    [Route("Home/Error/{code?}")]
    public IActionResult Error(string? code = null)
    {
        ViewBag.Code = code;
        return View();
    }
}
