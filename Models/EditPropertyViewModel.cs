using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class EditPropertyViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Property title is required")]
    [StringLength(200, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Property description is required")]
    [StringLength(5000, MinimumLength = 20)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(1000, 1000000000)]
    public decimal Price { get; set; }

    public PropertyType PropertyType { get; set; }

    [Range(50, 100000)]
    public double SquareFeet { get; set; }

    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    public string ZipCode { get; set; } = string.Empty;

    public VerificationStatus VerificationStatus { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public string? RejectionReason { get; set; }

    // Existing images
    public List<string> ExistingImageUrls { get; set; } = new();

    // New images (optional on edit)
    public List<IFormFile> NewImages { get; set; } = new();

    // Optional replacement verification documents
    public IFormFile? NewNidDocument { get; set; }
    public IFormFile? NewPropertyDocument { get; set; }
}
