using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PropLink.Application.Common.Interfaces;
using PropLink.Application.Common.Models;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;

namespace PropLink.Infrastructure.Services;

public sealed class PropertyRecommendationService : IPropertyRecommendationService
{
    private const int BudgetWeight = 25;
    private const int LocationWeight = 20;
    private const int PropertyTypeWeight = 10;
    private const int BedroomsWeight = 10;
    private const int BathroomsWeight = 5;
    private const int AreaWeight = 10;
    private const int ParkingWeight = 5;
    private const int SchoolWeight = 5;
    private const int HospitalWeight = 5;
    private const int TransportWeight = 5;
    private const int TotalWeight = 100;

    private readonly ApplicationDbContext _context;

    public PropertyRecommendationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PropertyRecommendationResult>> FindBestMatchesAsync(
        PropertyRecommendationCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var properties = await _context.Properties
            .AsNoTracking()
            .Where(property => property.VerificationStatus == VerificationStatus.Approved)
            .Select(property => new PropertyRecommendationListing
            {
                Id = property.Id,
                Title = property.Title,
                Description = property.Description,
                Price = property.Price,
                PropertyType = property.PropertyType,
                Address = property.Address,
                City = property.City,
                State = property.State,
                ZipCode = property.ZipCode,
                Bedrooms = property.Bedrooms,
                Bathrooms = property.Bathrooms,
                SquareFeet = property.SquareFeet,
                TransactionStatus = property.TransactionStatus,
                ParkingAvailable = property.ParkingAvailable,
                NearSchoolOrUniversity = property.NearSchoolOrUniversity,
                NearHospital = property.NearHospital,
                NearPublicTransport = property.NearPublicTransport,
                ImageUrl = property.Images
                    .OrderBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return properties
            .Select(property => ScoreProperty(property, criteria))
            .OrderByDescending(result => result.MatchPercentage)
            .ThenByDescending(result => result.DataCoveragePercentage)
            .ThenBy(result => result.Property.Price)
            .ThenBy(result => result.Property.Id)
            .ToList();
    }

    private static PropertyRecommendationResult ScoreProperty(
        PropertyRecommendationListing property,
        PropertyRecommendationCriteria criteria)
    {
        var details = new List<PropertyMatchDetail>
        {
            ScoreBudget(property.Price, criteria.MaximumBudget),
            ScoreLocation(property, criteria.Location),
            ScorePropertyType(property.PropertyType, criteria.PropertyType),
            ScoreMinimum("Bedrooms", BedroomsWeight, property.Bedrooms, criteria.MinimumBedrooms),
            ScoreMinimum("Bathrooms", BathroomsWeight, property.Bathrooms, criteria.MinimumBathrooms),
            ScoreMinimum("Area", AreaWeight, property.SquareFeet, criteria.MinimumArea),
            ScoreFacility("Parking", ParkingWeight, criteria.ParkingRequired, property.ParkingAvailable),
            ScoreFacility("School / university", SchoolWeight, criteria.NearSchoolOrUniversity, property.NearSchoolOrUniversity),
            ScoreFacility("Hospital", HospitalWeight, criteria.NearHospital, property.NearHospital),
            ScoreFacility("Public transport", TransportWeight, criteria.NearPublicTransport, property.NearPublicTransport)
        };

        var assessedWeight = details.Where(detail => detail.IsAssessed).Sum(detail => detail.Weight);
        var awardedPoints = details.Where(detail => detail.IsAssessed).Sum(detail => detail.PointsAwarded);
        var matchPercentage = assessedWeight == 0
            ? 0
            : (int)Math.Round(awardedPoints / assessedWeight * 100, MidpointRounding.AwayFromZero);

        return new PropertyRecommendationResult
        {
            Property = property,
            MatchPercentage = Math.Clamp(matchPercentage, 0, 100),
            DataCoveragePercentage = (int)Math.Round(assessedWeight * 100d / TotalWeight, MidpointRounding.AwayFromZero),
            Details = details
        };
    }

    private static PropertyMatchDetail ScoreBudget(decimal price, decimal maximumBudget)
    {
        var isWithinBudget = price <= maximumBudget;
        return new PropertyMatchDetail
        {
            Name = "Budget",
            Weight = BudgetWeight,
            PointsAwarded = isWithinBudget ? BudgetWeight : 0,
            IsAssessed = price > 0,
            Explanation = price <= 0
                ? "Price is not available."
                : isWithinBudget
                    ? $"Within your maximum budget of {maximumBudget:N0}."
                    : $"Over your maximum budget by {price - maximumBudget:N0}."
        };
    }

    private static PropertyMatchDetail ScoreLocation(PropertyRecommendationListing property, string requestedLocation)
    {
        var locationText = string.Join(" ", property.Address, property.City, property.State, property.ZipCode);
        var normalizedRequested = Normalize(requestedLocation);
        var normalizedLocation = Normalize(locationText);
        var requestedWords = normalizedRequested.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();

        if (string.IsNullOrWhiteSpace(normalizedLocation) || requestedWords.Length == 0)
        {
            return new PropertyMatchDetail
            {
                Name = "Location",
                Weight = LocationWeight,
                IsAssessed = false,
                Explanation = "Location details are not available and were excluded from the score."
            };
        }

        var locationWords = normalizedLocation.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var matchingWords = requestedWords.Count(locationWords.Contains);
        var matchRatio = (double)matchingWords / requestedWords.Length;
        var awardedPoints = LocationWeight * matchRatio;

        return new PropertyMatchDetail
        {
            Name = "Location",
            Weight = LocationWeight,
            PointsAwarded = awardedPoints,
            IsAssessed = true,
            Explanation = matchRatio >= 1
                ? $"Matches your preferred location: {requestedLocation.Trim()}."
                : matchingWords > 0
                    ? $"Partially matches your preferred location ({matchingWords} of {requestedWords.Length} terms)."
                    : "Does not match your preferred location."
        };
    }

    private static PropertyMatchDetail ScorePropertyType(PropertyType propertyType, PropertyType? requestedType)
    {
        var isMatch = !requestedType.HasValue || propertyType == requestedType.Value;
        return new PropertyMatchDetail
        {
            Name = "Property type",
            Weight = PropertyTypeWeight,
            PointsAwarded = isMatch ? PropertyTypeWeight : 0,
            IsAssessed = true,
            Explanation = !requestedType.HasValue
                ? "No property type preference was set."
                : isMatch
                    ? $"Matches your preferred type: {requestedType.Value}."
                    : $"This is {propertyType}, not your preferred type ({requestedType.Value})."
        };
    }

    private static PropertyMatchDetail ScoreMinimum(string name, int weight, int actual, int? minimum)
    {
        if (!minimum.HasValue || minimum.Value <= 0)
        {
            return new PropertyMatchDetail
            {
                Name = name,
                Weight = weight,
                PointsAwarded = weight,
                IsAssessed = true,
                Explanation = $"No minimum {name.ToLowerInvariant()} requirement was set."
            };
        }

        var ratio = Math.Clamp((double)actual / minimum.Value, 0, 1);
        return new PropertyMatchDetail
        {
            Name = name,
            Weight = weight,
            PointsAwarded = weight * ratio,
            IsAssessed = true,
            Explanation = actual >= minimum.Value
                ? $"Meets your minimum of {minimum.Value}."
                : $"Has {actual}; your minimum is {minimum.Value}."
        };
    }

    private static PropertyMatchDetail ScoreMinimum(string name, int weight, double actual, double? minimum)
    {
        if (!minimum.HasValue || minimum.Value <= 0)
        {
            return new PropertyMatchDetail
            {
                Name = name,
                Weight = weight,
                PointsAwarded = weight,
                IsAssessed = true,
                Explanation = $"No minimum {name.ToLowerInvariant()} requirement was set."
            };
        }

        var ratio = Math.Clamp(actual / minimum.Value, 0, 1);
        return new PropertyMatchDetail
        {
            Name = name,
            Weight = weight,
            PointsAwarded = weight * ratio,
            IsAssessed = true,
            Explanation = actual >= minimum.Value
                ? $"Meets your minimum of {minimum.Value:N0} sq.ft."
                : $"Has {actual:N0} sq.ft.; your minimum is {minimum.Value:N0} sq.ft."
        };
    }

    private static PropertyMatchDetail ScoreFacility(string name, int weight, bool requested, bool? available)
    {
        if (!requested)
        {
            return new PropertyMatchDetail
            {
                Name = name,
                Weight = weight,
                PointsAwarded = weight,
                IsAssessed = true,
                Explanation = "No preference was set."
            };
        }

        if (!available.HasValue)
        {
            return new PropertyMatchDetail
            {
                Name = name,
                Weight = weight,
                IsAssessed = false,
                Explanation = "This detail has not been provided and was excluded from the score."
            };
        }

        return new PropertyMatchDetail
        {
            Name = name,
            Weight = weight,
            PointsAwarded = available.Value ? weight : 0,
            IsAssessed = true,
            Explanation = available.Value ? $"{name} preference is met." : $"{name} preference is not met."
        };
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    }
}
