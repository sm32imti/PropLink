using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class PropertyCompareItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice => Price.ToString("C0");
    public PropertyType PropertyType { get; set; }
    
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double SquareFeet { get; set; }

    public decimal PricePerSquareFoot => SquareFeet > 0 ? Math.Round(Price / (decimal)SquareFeet, 2) : 0;
    public string FormattedPricePerSquareFoot => PricePerSquareFoot > 0 ? $"${PricePerSquareFoot:N0}/sqft" : "N/A";

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Approved;
    public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.Available;
    public DateTime CreatedAt { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();

    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;
    public string SellerPhone { get; set; } = string.Empty;
    public DateTime SellerMemberSince { get; set; }
    public int SellerTotalProperties { get; set; }

    public List<string> VerifiedDocumentTypes { get; set; } = new();

    // Smart Comparative Highlights
    public bool IsLowestPrice { get; set; }
    public bool IsHighestPrice { get; set; }
    public bool IsBestPricePerSqFt { get; set; }
    public bool IsLargestSquareFeet { get; set; }
    public bool IsMostBedrooms { get; set; }
    public bool IsMostBathrooms { get; set; }

    public decimal PriceDifferenceFromLowest { get; set; }
    public double PriceDifferencePercentFromLowest { get; set; }
}

public class PropertyCompareViewModel
{
    public List<PropertyCompareItemViewModel> Properties { get; set; } = new();
    public List<PropertyCardViewModel> AvailableApprovedProperties { get; set; } = new();
    public int MaxPropertiesToCompare { get; set; } = 5;

    public string ShareableQueryIds => string.Join(",", Properties.Select(p => p.Id));
    public bool HasProperties => Properties.Count > 0;
    public bool HasMultipleProperties => Properties.Count >= 2;

    public decimal LowestPrice => Properties.Any() ? Properties.Min(p => p.Price) : 0;
    public decimal HighestPrice => Properties.Any() ? Properties.Max(p => p.Price) : 0;
    public decimal AveragePrice => Properties.Any() ? Properties.Average(p => p.Price) : 0;
    public double AverageSquareFeet => Properties.Any() ? Properties.Average(p => p.SquareFeet) : 0;

    // Smart Buyer Preference Winners
    public PropertyCompareItemViewModel? LowestPriceProperty => Properties.OrderBy(p => p.Price).FirstOrDefault();
    public PropertyCompareItemViewModel? BestPricePerSqFtProperty => Properties.Where(p => p.PricePerSquareFoot > 0).OrderBy(p => p.PricePerSquareFoot).FirstOrDefault();
    public PropertyCompareItemViewModel? MostBedroomsProperty => Properties.OrderByDescending(p => p.Bedrooms).ThenByDescending(p => p.SquareFeet).FirstOrDefault();
    public PropertyCompareItemViewModel? MostBathroomsProperty => Properties.OrderByDescending(p => p.Bathrooms).ThenByDescending(p => p.SquareFeet).FirstOrDefault();
    public PropertyCompareItemViewModel? LargestSquareFeetProperty => Properties.OrderByDescending(p => p.SquareFeet).FirstOrDefault();
}
