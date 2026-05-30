using System.ComponentModel.DataAnnotations;

namespace QrMenu.Application.DTOs;

public class RestaurantDto
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
    public string? OwnerName { get; set; }
    public bool IsActive { get; set; }
    public bool IsApproved { get; set; }
    public string? ThemeColor { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CategoryCount { get; set; }
    public int ItemCount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string CurrencySymbol { get; set; } = "₺";
    public string? OpeningHoursJson { get; set; }
    public string SupportedLanguages { get; set; } = "tr";
}

public class CreateRestaurantDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? ThemeColor { get; set; } = "#e85d26";
    public string CurrencyCode { get; set; } = "TRY";
    public string CurrencySymbol { get; set; } = "₺";
    public string? OpeningHoursJson { get; set; }
    public string SupportedLanguages { get; set; } = "tr";
}

public class UpdateRestaurantDto : CreateRestaurantDto
{
    public int Id { get; set; }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; }
    public int RestaurantId { get; set; }
    public int ItemCount { get; set; }
    public List<MenuItemDto> Items { get; set; } = new();
}

public class CreateCategoryDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public int RestaurantId { get; set; }
    public int DisplayOrder { get; set; }
}

public class MenuItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsSoldOut { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsDailySpecial { get; set; }
    public string? Allergens { get; set; }
    public string? Tags { get; set; }
    public int DisplayOrder { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
}

public class CreateMenuItemDto
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    [StringLength(600)]
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    [Required, Range(0, 99999)]
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsSoldOut { get; set; } = false;
    public bool IsFeatured { get; set; }
    public bool IsDailySpecial { get; set; }
    public string? Allergens { get; set; }
    public string? Tags { get; set; }
    public int CategoryId { get; set; }
    public int DisplayOrder { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public int RestaurantCount { get; set; }
}

public class PublicMenuDto
{
    public RestaurantDto Restaurant { get; set; } = null!;
    public List<CategoryDto> Categories { get; set; } = new();
}

public class OpeningHours
{
    public string? Monday { get; set; }
    public string? Tuesday { get; set; }
    public string? Wednesday { get; set; }
    public string? Thursday { get; set; }
    public string? Friday { get; set; }
    public string? Saturday { get; set; }
    public string? Sunday { get; set; }
}
