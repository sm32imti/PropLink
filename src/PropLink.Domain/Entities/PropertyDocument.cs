using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class PropertyDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    
    public string DocumentType { get; set; } = string.Empty; // e.g., "NID", "Deed", "TaxReceipt", "UtilityBill", "IdentityProof"
    public string FileName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty; // Cloud storage object key
    public string FilePath { get; set; } = string.Empty; // Optional URI or path reference
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public string? ReviewRemarks { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}
