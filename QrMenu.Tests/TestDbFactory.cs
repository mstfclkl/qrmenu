using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Data;

namespace QrMenu.Tests;

public static class TestDbFactory
{
    public static AppDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static (UserManager<ApplicationUser>, AppDbContext) CreateSharedContext()
    {
        var db = CreateInMemory();
        return (BuildUserManager(db), db);
    }

    public static UserManager<ApplicationUser> BuildUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var opts = Options.Create(new IdentityOptions
        {
            Password = { RequireDigit=true, RequiredLength=8, RequireUppercase=true, RequireLowercase=true, RequireNonAlphanumeric=true }
        });
        return new UserManager<ApplicationUser>(store, opts, new PasswordHasher<ApplicationUser>(),
            new[] { new UserValidator<ApplicationUser>() }, new[] { new PasswordValidator<ApplicationUser>() },
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            new Logger<UserManager<ApplicationUser>>(new LoggerFactory()));
    }

    public static async Task<ApplicationUser> CreateTestOwner(UserManager<ApplicationUser> um, string email = "owner@test.com")
    {
        var user = new ApplicationUser { UserName=email, Email=email, FirstName="Test", LastName="Owner", EmailConfirmed=true, IsActive=true };
        await um.CreateAsync(user, "Test@1234!");
        return user;
    }

    public static async Task<(ApplicationUser user, string password)> CreateVerifiedUser(UserManager<ApplicationUser> um, string email = "verified@test.com", bool isActive = true)
    {
        const string pw = "Test@1234!";
        var user = new ApplicationUser { UserName=email, Email=email, FirstName="Verified", LastName="User", EmailConfirmed=true, IsActive=isActive };
        await um.CreateAsync(user, pw);
        return (user, pw);
    }

    public static async Task<(ApplicationUser user, string password)> CreateUnverifiedUser(UserManager<ApplicationUser> um, string email = "unverified@test.com")
    {
        const string pw = "Test@1234!";
        var user = new ApplicationUser { UserName=email, Email=email, FirstName="Unverified", LastName="User", EmailConfirmed=false, IsActive=true };
        await um.CreateAsync(user, pw);
        return (user, pw);
    }

    public static async Task<Restaurant> CreateTestRestaurant(AppDbContext db, string ownerId, bool approved = true)
    {
        var r = new Restaurant { Name="Test Restaurant", Slug="test-"+Guid.NewGuid().ToString("N")[..6], OwnerId=ownerId, IsActive=true, IsApproved=approved, CurrencyCode="TRY", CurrencySymbol="₺", SupportedLanguages="tr" };
        db.Restaurants.Add(r); await db.SaveChangesAsync(); return r;
    }
}
