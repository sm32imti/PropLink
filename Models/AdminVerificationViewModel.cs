using System.ComponentModel.DataAnnotations;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class AdminVerificationDashboardViewModel
{
    public List<AdminPendingPropertyItemViewModel> PendingProperties { get; set; } = new();
    public int TotalPendingCount { get; set; }
    public int TotalApprovedCount { get; set; }
    public int TotalRejectedCount { get; set; }

    // Bidding Requests Queue
    public List<AdminBiddingRequestItemViewModel> BiddingRequests { get; set; } = new();
    public int TotalBiddingPendingCount { get; set; }
    public int TotalBiddingApprovedCount { get; set; }
    public int TotalBiddingRejectedCount { get; set; }
    public string ActiveTab { get; set; } = "properties"; // "properties" or "bidding"
}

public class AdminPendingPropertyItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public PropertyType PropertyType { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double SquareFeet { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public DateTime SubmittedAt { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }

    // Seller Information
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;
    public string SellerPhone { get; set; } = string.Empty;

    // Gallery Photos
    public List<string> ImageUrls { get; set; } = new();

    // Sensitive Verification Documents (Accessible only to Admin)
    public List<AdminDocumentItemViewModel> Documents { get; set; } = new();
}

public class AdminDocumentItemViewModel
{
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // "NID", "Deed", etc.
    public string FileName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty;
    public string SecureViewUrl { get; set; } = string.Empty;
    public string SecureDownloadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class RejectPropertyRequestModel
{
    public Guid PropertyId { get; set; }

    [Required(ErrorMessage = "Please specify a detailed rejection reason so the seller can correct and resubmit.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Rejection reason must be between 5 and 1000 characters.")]
    public string RejectionReason { get; set; } = string.Empty;
}
