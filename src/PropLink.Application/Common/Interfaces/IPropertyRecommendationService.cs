using PropLink.Application.Common.Models;

namespace PropLink.Application.Common.Interfaces;

public interface IPropertyRecommendationService
{
    Task<IReadOnlyList<PropertyRecommendationResult>> FindBestMatchesAsync(
        PropertyRecommendationCriteria criteria,
        CancellationToken cancellationToken = default);
}
