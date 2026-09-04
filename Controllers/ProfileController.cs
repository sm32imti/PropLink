using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdStr, out var id) ? id : null;
        }
    }

    // ==========================================
    // 1. USER PROFILE DASHBOARD (/profile)
    // ==========================================
    [HttpGet]
    [Route("profile")]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
        {
            return Challenge();
        }

        // Fetch authenticated user info
        var user = await _context.Users.FindAsync(userId.Value);
        var userFullName = user?.FullName ?? User.Identity?.Name ?? "PropLink Member";
        var userEmail = user?.Email ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var userPhone = user?.PhoneNumber ?? "+1-555-0144";
        var userRole = user?.Role ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        var memberSince = user?.CreatedAt ?? DateTime.UtcNow;

        // 1. Fetch authenticated user's SELLING / LISTED PROPERTIES
        var userProperties = await _context.Properties
            .Include(p => p.Images)
            .Where(p => p.SellerId == userId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var sellingHistory = userProperties.Select(p => new MyPropertyListingViewModel
        {
            Id = p.Id,
            Title = p.Title,
            Price = p.Price,
            PropertyType = p.PropertyType,
            Location = $"{p.City}, {p.State}",
            MainImageUrl = p.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl
                           ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            VerificationStatus = p.VerificationStatus,
            TransactionStatus = p.TransactionStatus,
            RejectionReason = p.RejectionReason,
            CreatedAt = p.CreatedAt
        }).ToList();

        // 2. Fetch authenticated user's BUYING / TRANSACTION HISTORY
        var userPurchases = await _context.PropertyTransactions
            .Include(t => t.Property)
                .ThenInclude(p => p!.Images)
            .Where(t => t.BuyerId == userId.Value)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var buyingHistory = userPurchases.Select(t => new PropertyTransactionViewModel
        {
            TransactionId = t.Id,
            PropertyId = t.PropertyId,
            PropertyTitle = t.Property?.Title ?? "Property Agreement",
            PropertyImageUrl = t.Property?.Images.FirstOrDefault()?.ImageUrl 
                               ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            AgreedPrice = t.AgreedPrice,
            Location = t.Property != null ? $"{t.Property.City}, {t.Property.State}" : "Prime Location",
            TransactionStatus = t.Status,
            Notes = t.Notes,
            TransactionDate = t.TransactionDate
        }).ToList();

        var viewModel = new ProfileViewModel
        {
            UserId = userId.Value,
            FullName = userFullName,
            Email = userEmail,
            PhoneNumber = userPhone,
            Role = userRole,
            MemberSince = memberSince,
            TotalListedProperties = sellingHistory.Count,
            VerifiedListingsCount = sellingHistory.Count(s => s.VerificationStatus == VerificationStatus.Approved),
            PendingListingsCount = sellingHistory.Count(s => s.VerificationStatus == VerificationStatus.Pending),
            RejectedListingsCount = sellingHistory.Count(s => s.VerificationStatus == VerificationStatus.Rejected),
            TotalPurchasesCount = buyingHistory.Count,
            SellingHistory = sellingHistory,
            BuyingHistory = buyingHistory
        };

        return View(viewModel);
    }
}
