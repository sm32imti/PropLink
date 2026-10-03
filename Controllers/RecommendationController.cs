using Microsoft.AspNetCore.Mvc;
using PropLink.Application.Common.Interfaces;
using PropLink.Application.Common.Models;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

public class RecommendationController : Controller
{
    private const string FallbackImageUrl = "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80";

    private readonly IPropertyRecommendationService _recommendationService;

    public RecommendationController(IPropertyRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpGet]
    [Route("recommendations")]
    public IActionResult Index()
    {
        return View(new PropertyRecommendationRequestViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("recommendations/results")]
    public async Task<IActionResult> Results(
        PropertyRecommendationRequestViewModel request,
        CancellationToken cancellationToken)
    {
        if (request.PropertyType.HasValue && !Enum.IsDefined(request.PropertyType.Value))
        {
            ModelState.AddModelError(nameof(request.PropertyType), "Select a valid property type.");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", request);
        }

        var criteria = new PropertyRecommendationCriteria
        {
            Location = request.Location.Trim(),
            MaximumBudget = request.MaximumBudget!.Value,
            PropertyType = request.PropertyType,
            MinimumBedrooms = request.MinimumBedrooms,
            MinimumBathrooms = request.MinimumBathrooms,
            MinimumArea = request.MinimumArea,
            ParkingRequired = request.ParkingRequired,
            NearSchoolOrUniversity = request.NearSchoolOrUniversity,
            NearHospital = request.NearHospital,
            NearPublicTransport = request.NearPublicTransport
        };

        var recommendations = await _recommendationService.FindBestMatchesAsync(criteria, cancellationToken);
        var viewModel = new PropertyRecommendationResultsViewModel
        {
            Preferences = request,
            Properties = recommendations.Select(result => new PropertyRecommendationCardViewModel
            {
                Id = result.Property.Id,
                Title = result.Property.Title,
                Description = result.Property.Description,
                Price = result.Property.Price,
                PropertyType = result.Property.PropertyType,
                Address = result.Property.Address,
                City = result.Property.City,
                State = result.Property.State,
                Bedrooms = result.Property.Bedrooms,
                Bathrooms = result.Property.Bathrooms,
                SquareFeet = result.Property.SquareFeet,
                TransactionStatus = result.Property.TransactionStatus,
                ParkingAvailable = result.Property.ParkingAvailable,
                ImageUrl = string.IsNullOrWhiteSpace(result.Property.ImageUrl) ? FallbackImageUrl : result.Property.ImageUrl,
                MatchPercentage = result.MatchPercentage,
                DataCoveragePercentage = result.DataCoveragePercentage,
                Details = result.Details
            }).ToList()
        };

        return View(viewModel);
    }
}
