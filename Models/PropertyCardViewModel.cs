using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class PropertyCardViewModel
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
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double SquareFeet { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Verified;
    public string SellerName { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = "Recently added";
}
