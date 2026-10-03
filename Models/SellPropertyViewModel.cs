using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class SellPropertyViewModel
{
    [Required(ErrorMessage = "Property title is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    [Display(Name = "Property Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Property description is required")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Please provide a detailed description (at least 10 characters)")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(1, 1000000000, ErrorMessage = "Please enter a valid listing price greater than $0")]
    [Display(Name = "Listing Price ($)")]
    public decimal Price { get; set; } = 500000;

    [Required(ErrorMessage = "Property type is required")]
    [Display(Name = "Property Type")]
    public PropertyType PropertyType { get; set; } = PropertyType.House;

    [Required(ErrorMessage = "Square footage is required")]
    [Range(1, 100000, ErrorMessage = "Area must be between 1 and 100,000 sq ft")]
    [Display(Name = "Property Size (Sq Ft)")]
    public double SquareFeet { get; set; } = 2500;

    [Range(0, 50, ErrorMessage = "Bedrooms must be between 0 and 50")]
    [Display(Name = "Bedrooms")]
    public int Bedrooms { get; set; } = 3;

    [Range(0, 50, ErrorMessage = "Bathrooms must be between 0 and 50")]
    [Display(Name = "Bathrooms")]
    public int Bathrooms { get; set; } = 2;

    [Required(ErrorMessage = "Street address is required")]
    [Display(Name = "Street Address")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State / Region is required")]
    [Display(Name = "State / Province")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zip / Postal code is required")]
    [Display(Name = "Zip Code")]
    public string ZipCode { get; set; } = string.Empty;

    // Uploaded Property Images
    [Display(Name = "Property Photos")]
    public List<IFormFile>? Images { get; set; } = new();

    // Required Verification Documents
    [Display(Name = "Seller National ID (NID) Document")]
    public IFormFile? NidDocument { get; set; }

    [Display(Name = "Title Deed / Ownership / Tax Document")]
    public IFormFile? PropertyDocument { get; set; }
}

