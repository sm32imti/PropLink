namespace PropLink.Web.Models;

public class HomeViewModel
{
    public List<PropertyCardViewModel> FeaturedProperties { get; set; } = new();
    public int TotalVerifiedListings { get; set; } = 4850;
    public int ActiveBuyers { get; set; } = 18200;
    public double AverageReviewHours { get; set; } = 12.5;
    public int TotalCities { get; set; } = 85;

    // Search filter inputs
    public string? SearchLocation { get; set; }
    public string? SelectedPropertyType { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
}
