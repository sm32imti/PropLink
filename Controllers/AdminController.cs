using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Application.Common.Interfaces;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        ICloudStorageService cloudStorageService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _cloudStorageService = cloudStorageService;
        _logger = logger;
    }

    // ==========================================
    // 1. ADMIN VERIFICATION DASHBOARD
    // ==========================================
    [HttpGet]
    [Route("admin/verifications")]
    public async Task<IActionResult> Verifications()
    {
        var pendingProperties = await _context.Properties
            .Include(p => p.Seller)
            .Include(p => p.Images)
            .Include(p => p.Documents)
            .Where(p => p.VerificationStatus == VerificationStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var totalPending = pendingProperties.Count;
        var totalApproved = await _context.Properties.CountAsync(p => p.VerificationStatus == VerificationStatus.Approved);
        var totalRejected = await _context.Properties.CountAsync(p => p.VerificationStatus == VerificationStatus.Rejected);

        var viewModel = new AdminVerificationDashboardViewModel
        {
            TotalPendingCount = totalPending,
            TotalApprovedCount = totalApproved,
            TotalRejectedCount = totalRejected,
            PendingProperties = pendingProperties.Select(p => new AdminPendingPropertyItemViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Price = p.Price,
                PropertyType = p.PropertyType,
                Address = p.Address,
                City = p.City,
                State = p.State,
                SquareFeet = p.SquareFeet,
                Bedrooms = p.Bedrooms,
                Bathrooms = p.Bathrooms,
                SubmittedAt = p.CreatedAt,
                VerificationStatus = p.VerificationStatus,
                RejectionReason = p.RejectionReason,
                SellerId = p.SellerId,
                SellerName = p.Seller?.FullName ?? "Unknown Seller",
                SellerEmail = p.Seller?.Email ?? "No email",
                SellerPhone = p.Seller?.PhoneNumber ?? "No phone",
                ImageUrls = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                Documents = p.Documents.Select(d => new AdminDocumentItemViewModel
                {
                    DocumentId = d.Id,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    StorageReference = d.StorageReference,
                    SecureViewUrl = Url.Action("ViewSecureDocument", "Admin", new { docId = d.Id, download = false }) ?? $"/admin/documents/view-secure?docId={d.Id}&download=false",
                    SecureDownloadUrl = Url.Action("ViewSecureDocument", "Admin", new { docId = d.Id, download = true }) ?? $"/admin/documents/view-secure?docId={d.Id}&download=true",
                    ContentType = d.ContentType,
                    FileSizeBytes = d.FileSizeBytes,
                    UploadedAt = d.UploadedAt
                }).ToList()
            }).ToList()
        };

        return View(viewModel);
    }

    // ==========================================
    // 2. APPROVE PROPERTY
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin/verifications/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, string? adminNotes)
    {
        var property = await _context.Properties
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
        {
            TempData["ErrorMessage"] = "Property submission not found.";
            return RedirectToAction(nameof(Verifications));
        }

        property.VerificationStatus = VerificationStatus.Approved;
        property.ListingStatus = ListingStatus.Approved;
        property.RejectionReason = null;
        property.ReviewedAt = DateTime.UtcNow;
        property.AdminReviewNotes = !string.IsNullOrWhiteSpace(adminNotes) 
            ? adminNotes 
            : $"Approved by Administrator ({User.Identity?.Name}) on {DateTime.UtcNow:g}. All legal title documents & NID verified.";

        foreach (var doc in property.Documents)
        {
            doc.Status = VerificationStatus.Approved;
            doc.VerifiedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Listing \"{property.Title}\" has been APPROVED and is now publicly visible in the marketplace!";
        return RedirectToAction(nameof(Verifications));
    }

    // ==========================================
    // 3. REJECT PROPERTY (WITH REASON)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("admin/verifications/{id:guid}/reject")]
    public async Task<IActionResult> Reject(RejectPropertyRequestModel model)
    {
        if (string.IsNullOrWhiteSpace(model.RejectionReason) || model.RejectionReason.Trim().Length < 5)
        {
            TempData["ErrorMessage"] = "Rejection requires a descriptive reason (at least 5 characters) for the seller.";
            return RedirectToAction(nameof(Verifications));
        }

        var property = await _context.Properties
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == model.PropertyId);

        if (property == null)
        {
            TempData["ErrorMessage"] = "Property submission not found.";
            return RedirectToAction(nameof(Verifications));
        }

        property.VerificationStatus = VerificationStatus.Rejected;
        property.ListingStatus = ListingStatus.Rejected;
        property.RejectionReason = model.RejectionReason.Trim();
        property.ReviewedAt = DateTime.UtcNow;
        property.AdminReviewNotes = $"Rejected by Administrator ({User.Identity?.Name}) on {DateTime.UtcNow:g}. Reason: {model.RejectionReason.Trim()}";

        foreach (var doc in property.Documents)
        {
            doc.Status = VerificationStatus.Rejected;
            doc.ReviewRemarks = model.RejectionReason.Trim();
        }

        await _context.SaveChangesAsync();

        TempData["ToastMessage"] = $"Listing \"{property.Title}\" has been marked as REJECTED. The seller can see the reason in their profile and resubmit.";
        return RedirectToAction(nameof(Verifications));
    }

    // ==========================================
    // 4. SECURE DOCUMENT PREVIEW & DOWNLOAD (PROTECTED)
    // ==========================================
    [HttpGet]
    [Route("admin/documents/view-secure")]
    [Route("admin/documents/preview/{docId:guid}")]
    [Route("admin/documents/download/{docId:guid}")]
    public async Task<IActionResult> ViewSecureDocument(Guid docId, bool download = false)
    {
        var document = await _context.PropertyDocuments
            .Include(d => d.Property)
            .FirstOrDefaultAsync(d => d.Id == docId);

        if (document == null)
        {
            return NotFound("Document not found in verification vault.");
        }

        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole("Admin");
        var isOwner = Guid.TryParse(currentUserIdStr, out var currentUserId) && document.Property?.SellerId == currentUserId;

        // Security check: Only Admin or Owner can access
        if (!isAdmin && !isOwner)
        {
            return Forbid();
        }

        // 1. Direct from Database (Supabase PostgreSQL bytea)
        if (document.FileData != null && document.FileData.Length > 0)
        {
            var mimeType = !string.IsNullOrWhiteSpace(document.ContentType) 
                ? document.ContentType 
                : (document.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) 
                    ? "application/pdf" 
                    : (document.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) 
                        ? "image/png" 
                        : (document.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || document.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                            ? "image/jpeg" 
                            : "application/octet-stream")));

            if (download)
            {
                return File(document.FileData, mimeType, document.FileName);
            }
            else
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{document.FileName}\"";
                return File(document.FileData, mimeType);
            }
        }

        // 2. Check external cloud storage service
        var fileResult = await _cloudStorageService.GetPrivateDocumentAsync(document.StorageReference);
        if (fileResult != null)
        {
            if (download)
            {
                return File(fileResult.Value.FileBytes, fileResult.Value.ContentType, fileResult.Value.FileName);
            }
            else
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileResult.Value.FileName}\"";
                return File(fileResult.Value.FileBytes, fileResult.Value.ContentType);
            }
        }

        // 3. Fallback for sample seed data
        var samplePdf = System.Text.Encoding.UTF8.GetBytes(
            $"%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000009 00000 n\n0000000058 00000 n\n0000000115 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n218\n%%EOF\n");

        if (download)
        {
            return File(samplePdf, "application/pdf", document.FileName);
        }
        else
        {
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{document.FileName}\"";
            return File(samplePdf, "application/pdf");
        }
    }
}
