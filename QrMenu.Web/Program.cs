using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Interfaces;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Data;
using QrMenu.Infrastructure.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=qrmenu.db"));

builder.Services.AddDefaultIdentity<ApplicationUser>(o =>
{
    o.Password.RequireDigit = true; o.Password.RequiredLength = 8;
    o.Password.RequireUppercase = true; o.Password.RequireLowercase = true;
    o.Password.RequireNonAlphanumeric = true; o.Password.RequiredUniqueChars = 4;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.AllowedForNewUsers = true;
    o.User.RequireUniqueEmail = true; o.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true;
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.Name = "QrMenu.Auth";
});

builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("login", x => { x.PermitLimit=5; x.Window=TimeSpan.FromMinutes(15); x.QueueProcessingOrder=QueueProcessingOrder.OldestFirst; x.QueueLimit=0; });
    o.AddFixedWindowLimiter("register", x => { x.PermitLimit=3; x.Window=TimeSpan.FromHours(1); x.QueueProcessingOrder=QueueProcessingOrder.OldestFirst; x.QueueLimit=0; });
    o.RejectionStatusCode = 429;
});

builder.Services.AddMemoryCache();
builder.Services.AddSession(o => { o.IdleTimeout=TimeSpan.FromMinutes(30); o.Cookie.HttpOnly=true; o.Cookie.IsEssential=true; });

builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IVerificationService, VerificationService>();
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPublicMenuService, PublicMenuService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IScanAnalyticsService, ScanAnalyticsService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(o => { o.Cookie.Name="QrMenu.CSRF"; o.Cookie.HttpOnly=true; });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db, um, rm);
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    ctx.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve uploads from Railway persistent volume at /data
if (Directory.Exists("/data/uploads"))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider("/data/uploads"),
        RequestPath = "/data-files"
    });
}
if (Directory.Exists("/data/qr"))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider("/data/qr"),
        RequestPath = "/qr"
    });
}

app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Railway sets PORT env var dynamically
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
