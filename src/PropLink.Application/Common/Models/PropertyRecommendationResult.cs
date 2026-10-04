namespace PropLink.Application.Common.Models;

public sealed class PropertyRecommendationResult
{
    public PropertyRecommendationListing Property { get; init; } = new();
    public int MatchPercentage { get; init; }
    public int DataCoveragePercentage { get; init; }
    public IReadOnlyList<PropertyMatchDetail> Details { get; init; } = Array.Empty<PropertyMatchDetail>();
}
