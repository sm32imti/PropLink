using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class PropertyDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    
    public string DocumentType { get; set; } = string.Empty; // e.g., "Deed", "TaxReceipt", "UtilityBill", "IdentityProof"
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;

    public VerificationStatus Status { get; set; } = VerificationStatus.PendingReview;
    public string? ReviewRemarks { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}
