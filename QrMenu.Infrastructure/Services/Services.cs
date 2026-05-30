using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QrMenu.Application.DTOs;
using QrMenu.Application.Interfaces;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Data;
namespace QrMenu.Infrastructure.Services;

public class RestaurantService : IRestaurantService
{
    private readonly AppDbContext _db;
    public RestaurantService(AppDbContext db) => _db = db;
    public async Task<List<RestaurantDto>> GetAllAsync() => await _db.Restaurants.Include(r=>r.Owner).Include(r=>r.Categories).ThenInclude(c=>c.MenuItems).Select(r=>Map(r)).ToListAsync();
    public async Task<List<RestaurantDto>> GetByOwnerAsync(string ownerId) => await _db.Restaurants.Include(r=>r.Owner).Include(r=>r.Categories).ThenInclude(c=>c.MenuItems).Where(r=>r.OwnerId==ownerId).Select(r=>Map(r)).ToListAsync();
    public async Task<RestaurantDto?> GetByIdAsync(int id) => await _db.Restaurants.Include(r=>r.Owner).Include(r=>r.Categories).ThenInclude(c=>c.MenuItems).Where(r=>r.Id==id).Select(r=>Map(r)).FirstOrDefaultAsync();
    public async Task<RestaurantDto?> GetBySlugAsync(string slug) => await _db.Restaurants.Include(r=>r.Owner).Include(r=>r.Categories).ThenInclude(c=>c.MenuItems).Where(r=>r.Slug==slug).Select(r=>Map(r)).FirstOrDefaultAsync();
    public async Task<RestaurantDto> CreateAsync(CreateRestaurantDto dto, string ownerId)
    {
        var slug = Slug(dto.Name);
        var n = await _db.Restaurants.CountAsync(r=>r.Slug.StartsWith(slug));
        if (n>0) slug=$"{slug}-{n+1}";
        var r = new Restaurant { Name=dto.Name, Slug=slug, Description=dto.Description, Phone=dto.Phone, Address=dto.Address, Website=dto.Website, ThemeColor=dto.ThemeColor??"#e85d26", OwnerId=ownerId, IsActive=true, IsApproved=false, CurrencyCode=dto.CurrencyCode, CurrencySymbol=dto.CurrencySymbol, OpeningHoursJson=dto.OpeningHoursJson, SupportedLanguages=dto.SupportedLanguages };
        _db.Restaurants.Add(r); await _db.SaveChangesAsync();
        return (await GetByIdAsync(r.Id))!;
    }
    public async Task<RestaurantDto> UpdateAsync(UpdateRestaurantDto dto)
    {
        var r = await _db.Restaurants.FindAsync(dto.Id) ?? throw new InvalidOperationException("Not found");
        r.Name=dto.Name; r.Description=dto.Description; r.Phone=dto.Phone; r.Address=dto.Address; r.Website=dto.Website;
        r.ThemeColor=dto.ThemeColor??r.ThemeColor; r.CurrencyCode=dto.CurrencyCode; r.CurrencySymbol=dto.CurrencySymbol;
        r.OpeningHoursJson=dto.OpeningHoursJson; r.SupportedLanguages=dto.SupportedLanguages; r.UpdatedAt=DateTime.UtcNow;
        await _db.SaveChangesAsync(); return (await GetByIdAsync(dto.Id))!;
    }
    public async Task<bool> DeleteAsync(int id) { var r=await _db.Restaurants.FindAsync(id); if(r==null)return false; _db.Restaurants.Remove(r); await _db.SaveChangesAsync(); return true; }
    public async Task<bool> ApproveAsync(int id) { var r=await _db.Restaurants.FindAsync(id); if(r==null)return false; r.IsApproved=true; await _db.SaveChangesAsync(); return true; }
    public async Task<bool> ToggleActiveAsync(int id) { var r=await _db.Restaurants.FindAsync(id); if(r==null)return false; r.IsActive=!r.IsActive; await _db.SaveChangesAsync(); return true; }
    public async Task<string?> GenerateQrCodeAsync(int id, string baseUrl) { var r=await _db.Restaurants.FindAsync(id); if(r==null)return null; r.QrCodeUrl=$"/qr/{r.Slug}.png"; await _db.SaveChangesAsync(); return $"{baseUrl}/menu/{r.Slug}"; }
    public async Task<bool> UpdateLogoAsync(int id, string url) { var r=await _db.Restaurants.FindAsync(id); if(r==null)return false; r.LogoUrl=url; await _db.SaveChangesAsync(); return true; }
    private static string Slug(string name) => System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant().Trim(),@"[^a-z0-9]+" ,"-").Trim('-');
    private static RestaurantDto Map(Restaurant r) => new() { Id=r.Id, Name=r.Name, Slug=r.Slug, Description=r.Description, LogoUrl=r.LogoUrl, CoverImageUrl=r.CoverImageUrl, Phone=r.Phone, Address=r.Address, Website=r.Website, OwnerId=r.OwnerId, OwnerName=r.Owner?.FullName, IsActive=r.IsActive, IsApproved=r.IsApproved, ThemeColor=r.ThemeColor, QrCodeUrl=r.QrCodeUrl, CreatedAt=r.CreatedAt, CategoryCount=r.Categories.Count, ItemCount=r.Categories.Sum(c=>c.MenuItems.Count), CurrencyCode=r.CurrencyCode, CurrencySymbol=r.CurrencySymbol, OpeningHoursJson=r.OpeningHoursJson, SupportedLanguages=r.SupportedLanguages };
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    public CategoryService(AppDbContext db) => _db = db;
    public async Task<List<CategoryDto>> GetByRestaurantAsync(int restaurantId, bool includeItems=false)
    {
        var q = _db.Categories.Where(c=>c.RestaurantId==restaurantId).OrderBy(c=>c.DisplayOrder);
        if (includeItems) return await q.Include(c=>c.MenuItems.OrderBy(m=>m.DisplayOrder)).Select(c=>Map(c,true)).ToListAsync();
        return await q.Select(c=>Map(c,false)).ToListAsync();
    }
    public async Task<CategoryDto?> GetByIdAsync(int id) => await _db.Categories.Include(c=>c.MenuItems).Where(c=>c.Id==id).Select(c=>Map(c,true)).FirstOrDefaultAsync();
    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var c = new Category { Name=dto.Name, NameEn=dto.NameEn, Description=dto.Description, DescriptionEn=dto.DescriptionEn, RestaurantId=dto.RestaurantId, DisplayOrder=dto.DisplayOrder, IsVisible=true };
        _db.Categories.Add(c); await _db.SaveChangesAsync(); return Map(c,false);
    }
    public async Task<CategoryDto> UpdateAsync(int id, CreateCategoryDto dto) { var c=await _db.Categories.FindAsync(id)??throw new InvalidOperationException(); c.Name=dto.Name; c.NameEn=dto.NameEn; c.Description=dto.Description; c.DescriptionEn=dto.DescriptionEn; await _db.SaveChangesAsync(); return (await GetByIdAsync(id))!; }
    public async Task<bool> DeleteAsync(int id) { var c=await _db.Categories.FindAsync(id); if(c==null)return false; _db.Categories.Remove(c); await _db.SaveChangesAsync(); return true; }
    public async Task<bool> ReorderAsync(int restaurantId, List<int> ids) { var cats=await _db.Categories.Where(c=>c.RestaurantId==restaurantId).ToListAsync(); for(int i=0;i<ids.Count;i++){var c=cats.FirstOrDefault(x=>x.Id==ids[i]);if(c!=null)c.DisplayOrder=i+1;} await _db.SaveChangesAsync(); return true; }
    private static CategoryDto Map(Category c, bool items) => new() { Id=c.Id, Name=c.Name, NameEn=c.NameEn, Description=c.Description, DescriptionEn=c.DescriptionEn, DisplayOrder=c.DisplayOrder, IsVisible=c.IsVisible, RestaurantId=c.RestaurantId, ItemCount=c.MenuItems.Count, Items=items?c.MenuItems.OrderBy(m=>m.DisplayOrder).Select(m=>MapItem(m)).ToList():new() };
    private static MenuItemDto MapItem(MenuItem m) => new() { Id=m.Id, Name=m.Name, NameEn=m.NameEn, Description=m.Description, DescriptionEn=m.DescriptionEn, Price=m.Price, ImageUrl=m.ImageUrl, IsAvailable=m.IsAvailable, IsSoldOut=m.IsSoldOut, IsFeatured=m.IsFeatured, IsDailySpecial=m.IsDailySpecial, Allergens=m.Allergens, Tags=m.Tags, DisplayOrder=m.DisplayOrder, CategoryId=m.CategoryId };
}

public class MenuItemService : IMenuItemService
{
    private readonly AppDbContext _db;
    public MenuItemService(AppDbContext db) => _db = db;
    public async Task<List<MenuItemDto>> GetByCategoryAsync(int categoryId) => await _db.MenuItems.Where(m=>m.CategoryId==categoryId).OrderBy(m=>m.DisplayOrder).Select(m=>Map(m)).ToListAsync();
    public async Task<MenuItemDto?> GetByIdAsync(int id) => await _db.MenuItems.Include(m=>m.Category).Where(m=>m.Id==id).Select(m=>Map(m)).FirstOrDefaultAsync();
    public async Task<MenuItemDto> CreateAsync(CreateMenuItemDto dto) { var m=new MenuItem{Name=dto.Name,NameEn=dto.NameEn,Description=dto.Description,DescriptionEn=dto.DescriptionEn,Price=dto.Price,IsAvailable=dto.IsAvailable,IsSoldOut=dto.IsSoldOut,IsFeatured=dto.IsFeatured,IsDailySpecial=dto.IsDailySpecial,Allergens=dto.Allergens,Tags=dto.Tags,CategoryId=dto.CategoryId,DisplayOrder=dto.DisplayOrder}; _db.MenuItems.Add(m); await _db.SaveChangesAsync(); return (await GetByIdAsync(m.Id))!; }
    public async Task<MenuItemDto> UpdateAsync(int id, CreateMenuItemDto dto) { var m=await _db.MenuItems.FindAsync(id)??throw new InvalidOperationException(); m.Name=dto.Name;m.NameEn=dto.NameEn;m.Description=dto.Description;m.DescriptionEn=dto.DescriptionEn;m.Price=dto.Price;m.IsAvailable=dto.IsAvailable;m.IsSoldOut=dto.IsSoldOut;m.IsFeatured=dto.IsFeatured;m.IsDailySpecial=dto.IsDailySpecial;m.Allergens=dto.Allergens;m.Tags=dto.Tags;m.CategoryId=dto.CategoryId; await _db.SaveChangesAsync(); return (await GetByIdAsync(id))!; }
    public async Task<bool> DeleteAsync(int id) { var m=await _db.MenuItems.FindAsync(id); if(m==null)return false; _db.MenuItems.Remove(m); await _db.SaveChangesAsync(); return true; }
    public async Task<bool> ToggleAvailabilityAsync(int id) { var m=await _db.MenuItems.FindAsync(id); if(m==null)return false; m.IsAvailable=!m.IsAvailable; await _db.SaveChangesAsync(); return true; }
    public async Task<bool> ToggleSoldOutAsync(int id) { var m=await _db.MenuItems.FindAsync(id); if(m==null)return false; m.IsSoldOut=!m.IsSoldOut; await _db.SaveChangesAsync(); return true; }
    public async Task<bool> UpdateImageAsync(int id, string url) { var m=await _db.MenuItems.FindAsync(id); if(m==null)return false; m.ImageUrl=url; await _db.SaveChangesAsync(); return true; }
    private static MenuItemDto Map(MenuItem m) => new() { Id=m.Id,Name=m.Name,NameEn=m.NameEn,Description=m.Description,DescriptionEn=m.DescriptionEn,Price=m.Price,ImageUrl=m.ImageUrl,IsAvailable=m.IsAvailable,IsSoldOut=m.IsSoldOut,IsFeatured=m.IsFeatured,IsDailySpecial=m.IsDailySpecial,Allergens=m.Allergens,Tags=m.Tags,DisplayOrder=m.DisplayOrder,CategoryId=m.CategoryId,CategoryName=m.Category?.Name };
}

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _um;
    public UserService(UserManager<ApplicationUser> um) => _um = um;
    public async Task<List<UserDto>> GetAllOwnersAsync() { var owners=await _um.GetUsersInRoleAsync("Owner"); var r=new List<UserDto>(); foreach(var u in owners) r.Add(new UserDto{Id=u.Id,Email=u.Email!,FullName=u.FullName,FirstName=u.FirstName,LastName=u.LastName,IsActive=u.IsActive,CreatedAt=u.CreatedAt,Roles=(await _um.GetRolesAsync(u)).ToList()}); return r; }
    public async Task<UserDto?> GetByIdAsync(string id) { var u=await _um.FindByIdAsync(id); if(u==null)return null; return new UserDto{Id=u.Id,Email=u.Email!,FullName=u.FullName,FirstName=u.FirstName,LastName=u.LastName,IsActive=u.IsActive,CreatedAt=u.CreatedAt,Roles=(await _um.GetRolesAsync(u)).ToList()}; }
    public async Task<bool> DeactivateAsync(string id) { var u=await _um.FindByIdAsync(id); if(u==null)return false; u.IsActive=false; await _um.UpdateAsync(u); return true; }
    public async Task<bool> ActivateAsync(string id) { var u=await _um.FindByIdAsync(id); if(u==null)return false; u.IsActive=true; await _um.UpdateAsync(u); return true; }
    public async Task<bool> AssignRoleAsync(string userId, string role) { var u=await _um.FindByIdAsync(userId); if(u==null)return false; await _um.AddToRoleAsync(u,role); return true; }
}

public class PublicMenuService : IPublicMenuService
{
    private readonly IRestaurantService _rs; private readonly ICategoryService _cs;
    public PublicMenuService(IRestaurantService rs, ICategoryService cs){_rs=rs;_cs=cs;}
    public async Task<PublicMenuDto?> GetPublicMenuAsync(string slug) { var r=await _rs.GetBySlugAsync(slug); if(r==null||!r.IsActive||!r.IsApproved)return null; var cats=await _cs.GetByRestaurantAsync(r.Id,true); return new PublicMenuDto{Restaurant=r,Categories=cats.Where(c=>c.IsVisible).ToList()}; }
}
