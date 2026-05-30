using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QrMenu.Domain.Entities;
namespace QrMenu.Infrastructure.Data;
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm)
    {
        foreach (var role in new[]{"Admin","Owner","Customer"})
            if (!await rm.RoleExistsAsync(role)) await rm.CreateAsync(new IdentityRole(role));
        const string adminEmail = "admin@qrmenu.com";
        if (await um.FindByEmailAsync(adminEmail) == null)
        {
            var a = new ApplicationUser { UserName=adminEmail, Email=adminEmail, FirstName="System", LastName="Admin", EmailConfirmed=true };
            if ((await um.CreateAsync(a,"Admin@123456!")).Succeeded) await um.AddToRoleAsync(a,"Admin");
        }
        const string ownerEmail = "owner@demo.com";
        ApplicationUser? owner;
        if (await um.FindByEmailAsync(ownerEmail) == null)
        {
            owner = new ApplicationUser { UserName=ownerEmail, Email=ownerEmail, FirstName="Demo", LastName="Owner", EmailConfirmed=true };
            if ((await um.CreateAsync(owner,"Owner@123456!")).Succeeded) await um.AddToRoleAsync(owner,"Owner");
        }
        else owner = await um.FindByEmailAsync(ownerEmail);
        if (owner != null && !await db.Restaurants.AnyAsync())
        {
            var r = new Restaurant { Name="Bella Cucina", Slug="bella-cucina", Description="Authentic Italian cuisine in the heart of the city.", Phone="+90 212 555 0100", Address="Bagdat Caddesi No:42, Istanbul", ThemeColor="#c0392b", IsActive=true, IsApproved=true, OwnerId=owner.Id };
            db.Restaurants.Add(r); await db.SaveChangesAsync();
            var s = new Category{Name="Starters",DisplayOrder=1,RestaurantId=r.Id};
            var m = new Category{Name="Main Courses",DisplayOrder=2,RestaurantId=r.Id};
            var d = new Category{Name="Desserts",DisplayOrder=3,RestaurantId=r.Id};
            var dr = new Category{Name="Drinks",DisplayOrder=4,RestaurantId=r.Id};
            db.Categories.AddRange(s,m,d,dr); await db.SaveChangesAsync();
            db.MenuItems.AddRange(
                new MenuItem{Name="Bruschetta al Pomodoro",Description="Toasted bread with fresh tomatoes, garlic, and basil",Price=12.50m,CategoryId=s.Id,IsFeatured=true,Tags="vegetarian",DisplayOrder=1},
                new MenuItem{Name="Burrata e Prosciutto",Description="Creamy burrata with aged prosciutto di Parma",Price=18.00m,CategoryId=s.Id,DisplayOrder=2},
                new MenuItem{Name="Zuppa del Giorno",Description="Chef soup of the day",Price=9.50m,CategoryId=s.Id,DisplayOrder=3},
                new MenuItem{Name="Tagliatelle al Ragu",Description="Fresh pasta with slow-cooked Bolognese sauce",Price=24.00m,CategoryId=m.Id,IsFeatured=true,DisplayOrder=1},
                new MenuItem{Name="Risotto ai Funghi Porcini",Description="Creamy Arborio rice with wild porcini mushrooms",Price=22.00m,CategoryId=m.Id,Tags="vegetarian",DisplayOrder=2},
                new MenuItem{Name="Branzino alla Griglia",Description="Grilled sea bass with lemon and capers",Price=32.00m,CategoryId=m.Id,DisplayOrder=3},
                new MenuItem{Name="Pollo alla Parmigiana",Description="Breaded chicken with tomato sauce and mozzarella",Price=26.00m,CategoryId=m.Id,DisplayOrder=4},
                new MenuItem{Name="Tiramisu",Description="Classic espresso and mascarpone dessert",Price=11.00m,CategoryId=d.Id,IsFeatured=true,DisplayOrder=1},
                new MenuItem{Name="Panna Cotta",Description="Vanilla cream with mixed berry coulis",Price=10.00m,CategoryId=d.Id,Tags="gluten-free",DisplayOrder=2},
                new MenuItem{Name="Acqua Minerale",Description="Still or sparkling water 500ml",Price=4.00m,CategoryId=dr.Id,DisplayOrder=1},
                new MenuItem{Name="Limonata Fresca",Description="Freshly squeezed lemonade",Price=7.00m,CategoryId=dr.Id,Tags="vegan",DisplayOrder=2},
                new MenuItem{Name="Caffe Espresso",Description="Double shot Italian espresso",Price=5.00m,CategoryId=dr.Id,DisplayOrder=3}
            );
            await db.SaveChangesAsync();
        }
    }
}
