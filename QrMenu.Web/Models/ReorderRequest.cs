namespace QrMenu.Web.Models;

public class ReorderRequest
{
    public int RestaurantId { get; set; }
    public List<int> OrderedIds { get; set; } = new();
}
