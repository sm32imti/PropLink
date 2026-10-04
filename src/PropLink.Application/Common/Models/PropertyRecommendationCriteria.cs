using PropLink.Domain.Enums;

namespace PropLink.Application.Common.Models;

public sealed class PropertyRecommendationCriteria
{
    public string Location { get; init; } = string.Empty;
    public decimal MaximumBudget { get; init; }
    public PropertyType? PropertyType { get; init; }
    public int? MinimumBedrooms { get; init; }
    public int? MinimumBathrooms { get; init; }
    public double? MinimumArea { get; init; }
    public bool ParkingRequired { get; init; }
    public bool NearSchoolOrUniversity { get; init; }
    public bool NearHospital { get; init; }
    public bool NearPublicTransport { get; init; }
}
