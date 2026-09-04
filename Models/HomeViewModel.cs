namespace PropLink.Web.Models;

public class HomeViewModel
{
    public int TotalVerifiedListings { get; set; } = 4850;
    public int ActiveBuyers { get; set; } = 19400;
    public double AverageReviewHours { get; set; } = 8.5;
    public int TotalCities { get; set; } = 92;

    // Search filter inputs
    public string? SearchLocation { get; set; }
    public string? SelectedPropertyType { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
}

