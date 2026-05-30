using Microsoft.AspNetCore.Identity;

namespace QrMenu.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
}

public class Restaurant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = false;
    public string? ThemeColor { get; set; } = "#e85d26";
    public string? QrCodeUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CurrencyCode { get; set; } = "TRY";
    public string CurrencySymbol { get; set; } = "₺";
    public string? OpeningHoursJson { get; set; }
    public string SupportedLanguages { get; set; } = "tr";
    public ApplicationUser? Owner { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public int RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsSoldOut { get; set; } = false;
    public bool IsFeatured { get; set; } = false;
    public bool IsDailySpecial { get; set; } = false;
    public string? Allergens { get; set; }
    public string? Tags { get; set; }
    public int DisplayOrder { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class EmailVerificationCode
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser? User { get; set; }
}

public class MenuScanLog
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public Restaurant? Restaurant { get; set; }
}
