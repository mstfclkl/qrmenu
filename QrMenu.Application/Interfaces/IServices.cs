using QrMenu.Application.DTOs;

namespace QrMenu.Application.Interfaces;

public interface IRestaurantService
{
    Task<List<RestaurantDto>> GetAllAsync();
    Task<List<RestaurantDto>> GetByOwnerAsync(string ownerId);
    Task<RestaurantDto?> GetByIdAsync(int id);
    Task<RestaurantDto?> GetBySlugAsync(string slug);
    Task<RestaurantDto> CreateAsync(CreateRestaurantDto dto, string ownerId);
    Task<RestaurantDto> UpdateAsync(UpdateRestaurantDto dto);
    Task<bool> DeleteAsync(int id);
    Task<bool> ApproveAsync(int id);
    Task<bool> ToggleActiveAsync(int id);
    Task<string?> GenerateQrCodeAsync(int restaurantId, string baseUrl);
    Task<bool> UpdateLogoAsync(int id, string logoUrl);
}

public interface ICategoryService
{
    Task<List<CategoryDto>> GetByRestaurantAsync(int restaurantId, bool includeItems = false);
    Task<CategoryDto?> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto> UpdateAsync(int id, CreateCategoryDto dto);
    Task<bool> DeleteAsync(int id);
    Task<bool> ReorderAsync(int restaurantId, List<int> orderedIds);
}

public interface IMenuItemService
{
    Task<List<MenuItemDto>> GetByCategoryAsync(int categoryId);
    Task<MenuItemDto?> GetByIdAsync(int id);
    Task<MenuItemDto> CreateAsync(CreateMenuItemDto dto);
    Task<MenuItemDto> UpdateAsync(int id, CreateMenuItemDto dto);
    Task<bool> DeleteAsync(int id);
    Task<bool> ToggleAvailabilityAsync(int id);
    Task<bool> ToggleSoldOutAsync(int id);
    Task<bool> UpdateImageAsync(int id, string imageUrl);
}

public interface IUserService
{
    Task<List<UserDto>> GetAllOwnersAsync();
    Task<UserDto?> GetByIdAsync(string id);
    Task<bool> DeactivateAsync(string id);
    Task<bool> ActivateAsync(string id);
    Task<bool> AssignRoleAsync(string userId, string role);
}

public interface IPublicMenuService
{
    Task<PublicMenuDto?> GetPublicMenuAsync(string slug);
}
