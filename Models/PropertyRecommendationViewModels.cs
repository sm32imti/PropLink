using System.ComponentModel.DataAnnotations;
using PropLink.Application.Common.Models;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public sealed class PropertyRecommendationRequestViewModel
{
    [Required(ErrorMessage = "Enter a preferred location.")]
    [StringLength(250, ErrorMessage = "Location must be 250 characters or fewer.")]
    [Display(Name = "Preferred Location")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your maximum budget.")]
    [Range(1, 1000000000, ErrorMessage = "Budget must be between 1 and 1,000,000,000.")]
    [Display(Name = "Maximum Budget")]
    public decimal? MaximumBudget { get; set; }

    [Display(Name = "Property Type")]
    public PropertyType? PropertyType { get; set; }

    [Range(0, 50, ErrorMessage = "Bedrooms must be between 0 and 50.")]
    [Display(Name = "Minimum Bedrooms")]
    public int? MinimumBedrooms { get; set; }

    [Range(0, 50, ErrorMessage = "Bathrooms must be between 0 and 50.")]
    [Display(Name = "Minimum Bathrooms")]
    public int? MinimumBathrooms { get; set; }

    [Range(0, 100000, ErrorMessage = "Area must be between 0 and 100,000 sq.ft.")]
    [Display(Name = "Minimum Area")]
    public double? MinimumArea { get; set; }

    [Display(Name = "Parking Required")]
    public bool ParkingRequired { get; set; }

    [Display(Name = "Near School or University")]
    public bool NearSchoolOrUniversity { get; set; }

    [Display(Name = "Near Hospital")]
    public bool NearHospital { get; set; }

    [Display(Name = "Near Public Transport")]
    public bool NearPublicTransport { get; set; }
}

public sealed class PropertyRecommendationResultsViewModel
{
    public PropertyRecommendationRequestViewModel Preferences { get; set; } = new();
    public List<PropertyRecommendationCardViewModel> Properties { get; set; } = new();
}

public sealed class PropertyRecommendationCardViewModel
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
    public TransactionStatus TransactionStatus { get; set; }
    public bool? ParkingAvailable { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int MatchPercentage { get; set; }
    public int DataCoveragePercentage { get; set; }
    public IReadOnlyList<PropertyMatchDetail> Details { get; set; } = Array.Empty<PropertyMatchDetail>();
}
