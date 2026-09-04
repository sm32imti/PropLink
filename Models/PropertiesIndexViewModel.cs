using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class PropertiesIndexViewModel
{
    public List<PropertyCardViewModel> Properties { get; set; } = new();

    // Filters
    public string? SearchQuery { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? City { get; set; }
    public string? SortBy { get; set; }

    // Backend Pagination
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 6;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}
