using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class UserReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Reporter Information
    public Guid ReporterId { get; set; }
    public User? Reporter { get; set; }

    // Suspect / Reported User Information
    public Guid ReportedUserId { get; set; }
    public User? ReportedUser { get; set; }

    // Optional Context (if reported from a property listing)
    public Guid? RelatedPropertyId { get; set; }
    public Property? RelatedProperty { get; set; }

    // Report Details
    public string Category { get; set; } = "Financial Fraud"; // e.g. Fake Title Deed, Payment Scam, Impersonation, Harassment
    public string Description { get; set; } = string.Empty;

    // Proof Document / Screenshot Evidence
    public string? ProofFileName { get; set; }
    public string? ProofContentType { get; set; }
    public byte[]? ProofFileData { get; set; }
    public long ProofFileSizeBytes { get; set; }
    public string? ProofStorageReference { get; set; }

    // Lifecycle & Admin Decision
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public User? ReviewedByAdmin { get; set; }
    public string? AdminDecisionNotes { get; set; }
}
