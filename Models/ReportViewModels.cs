using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class SubmitUserReportViewModel
{
    [Required]
    public Guid ReportedUserId { get; set; }

    public string? ReportedUserName { get; set; }
    public string? ReportedUserEmail { get; set; }

    public Guid? RelatedPropertyId { get; set; }
    public string? RelatedPropertyTitle { get; set; }

    [Required(ErrorMessage = "Please select a fraud category")]
    [Display(Name = "Fraud Category")]
    public string Category { get; set; } = "Fake Title Deed / Forgery";

    [Required(ErrorMessage = "Please provide detailed description of the fraud incident")]
    [MinLength(15, ErrorMessage = "Description must be at least 15 characters")]
    [MaxLength(4000)]
    [Display(Name = "Incident Details & Reason")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Evidence / Proof File (Images, PDF, Receipts, Chat Screenshots)")]
    public IFormFile? ProofFile { get; set; }

    public string? ReturnUrl { get; set; }
}

public class AdminReportDashboardViewModel
{
    public int TotalPendingReports { get; set; }
    public int TotalBannedUsers { get; set; }
    public int TotalDismissedReports { get; set; }
    public string ActiveTab { get; set; } = "pending"; // "pending", "banned", "dismissed"

    public List<AdminReportItemViewModel> Reports { get; set; } = new();
}

public class AdminReportItemViewModel
{
    public Guid ReportId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReportStatus Status { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Reporter Details
    public Guid ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterEmail { get; set; } = string.Empty;

    // Reported / Suspect User Details
    public Guid ReportedUserId { get; set; }
    public string ReportedUserName { get; set; } = string.Empty;
    public string ReportedUserEmail { get; set; } = string.Empty;
    public string ReportedUserPhone { get; set; } = string.Empty;
    public string? ReportedUserNid { get; set; }
    public DateTime ReportedUserMemberSince { get; set; }
    public bool ReportedUserIsBanned { get; set; }
    public DateTime? ReportedUserBannedAt { get; set; }
    public string? ReportedUserBanReason { get; set; }
    public int ReportedUserActiveListingsCount { get; set; }

    // Associated Listing context
    public Guid? RelatedPropertyId { get; set; }
    public string? RelatedPropertyTitle { get; set; }

    // Evidence / Proof
    public string? ProofFileName { get; set; }
    public string? ProofContentType { get; set; }
    public long ProofFileSizeBytes { get; set; }
    public bool HasProofFile { get; set; }
    public string? SecureProofViewUrl { get; set; }
    public string? SecureProofDownloadUrl { get; set; }

    // Admin Review Details
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByAdminName { get; set; }
    public string? AdminDecisionNotes { get; set; }
}

public class BanUserRequestModel
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid ReportedUserId { get; set; }

    [Required(ErrorMessage = "Please specify a direct ban reason")]
    [MinLength(5, ErrorMessage = "Ban reason must be at least 5 characters")]
    public string BanReason { get; set; } = string.Empty;

    public string? AdminNotes { get; set; }
}

public class DismissReportRequestModel
{
    [Required]
    public Guid ReportId { get; set; }

    [MaxLength(2000)]
    public string? DismissNotes { get; set; }
}

public class PublicUserProfileViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NidNumber { get; set; }
    public DateTime MemberSince { get; set; }
    public string Role { get; set; } = "User";
    public bool IsBanned { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? BanReason { get; set; }

    public int TotalPropertiesListed { get; set; }
    public int TotalVerifiedProperties { get; set; }
    public int TotalAuctionsHosted { get; set; }

    public List<PropertyCardViewModel> ActiveProperties { get; set; } = new();
}
