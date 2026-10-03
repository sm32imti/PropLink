namespace PropLink.Application.Common.Models;

public sealed class PropertyMatchDetail
{
    public string Name { get; init; } = string.Empty;
    public int Weight { get; init; }
    public double PointsAwarded { get; init; }
    public bool IsAssessed { get; init; }
    public string Explanation { get; init; } = string.Empty;
}
