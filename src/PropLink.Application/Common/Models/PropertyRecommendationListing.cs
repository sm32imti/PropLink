using PropLink.Domain.Enums;

namespace PropLink.Application.Common.Models;

public sealed class PropertyRecommendationListing
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public PropertyType PropertyType { get; init; }
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string ZipCode { get; init; } = string.Empty;
    public int Bedrooms { get; init; }
    public int Bathrooms { get; init; }
    public double SquareFeet { get; init; }
    public TransactionStatus TransactionStatus { get; init; }
    public bool? ParkingAvailable { get; init; }
    public bool? NearSchoolOrUniversity { get; init; }
    public bool? NearHospital { get; init; }
    public bool? NearPublicTransport { get; init; }
    public string? ImageUrl { get; init; }
}
