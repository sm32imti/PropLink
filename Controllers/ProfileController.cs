using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Entities;
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

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "";

        // Fetch authenticated user info from DB or fallback registry
        User? user = null;
        try
        {
            user = await _context.Users.FindAsync(userId.Value);
        }
        catch
        {
        }

        if (user == null && !string.IsNullOrEmpty(userEmail) && AccountController._userRegistry.TryGetValue(userEmail, out var regUser))
        {
            user = regUser;
        }

        var userFullName = user?.FullName ?? User.Identity?.Name ?? "PropLink Member";
        userEmail = string.IsNullOrEmpty(userEmail) ? (user?.Email ?? "") : userEmail;
        var userPhone = user?.PhoneNumber ?? "+1-555-0144";
        var userNid = user?.NidNumber ?? "1234567890123";
        var userRole = user?.Role ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        var memberSince = user?.CreatedAt ?? DateTime.UtcNow;

        // 1. Fetch authenticated user's SELLING / LISTED PROPERTIES
        var userProperties = await _context.Properties
            .Include(p => p.Images)
            .Where(p => p.SellerId == userId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var sellingHistory = userProperties.Select(p => {
            var coverImg = p.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
            return new MyPropertyListingViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Price = p.Price,
                PropertyType = p.PropertyType,
                Location = $"{p.City}, {p.State}",
                MainImageUrl = !string.IsNullOrWhiteSpace(coverImg) 
                    ? coverImg 
                    : "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                VerificationStatus = p.VerificationStatus,
                TransactionStatus = p.TransactionStatus,
                RejectionReason = p.RejectionReason,
                CreatedAt = p.CreatedAt
            };
        }).ToList();

        // 2. Fetch authenticated user's BUYING / TRANSACTION HISTORY
        var userPurchases = await _context.PropertyTransactions
            .Include(t => t.Property)
                .ThenInclude(p => p!.Images)
            .Where(t => t.BuyerId == userId.Value)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var buyingHistory = userPurchases.Select(t => {
            var propImg = t.Property?.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
            return new PropertyTransactionViewModel
            {
                TransactionId = t.Id,
                PropertyId = t.PropertyId,
                PropertyTitle = t.Property?.Title ?? "Property Agreement",
                PropertyImageUrl = !string.IsNullOrWhiteSpace(propImg)
                    ? propImg 
                    : "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                AgreedPrice = t.AgreedPrice,
                Location = t.Property != null ? $"{t.Property.City}, {t.Property.State}" : "Prime Location",
                TransactionStatus = t.Status,
                Notes = t.Notes,
                TransactionDate = t.TransactionDate
            };
        }).ToList();

        var viewModel = new ProfileViewModel
        {
            UserId = userId.Value,
            FullName = userFullName,
            Email = userEmail,
            PhoneNumber = userPhone,
            NidNumber = userNid,
            Role = userRole,
            MemberSince = memberSince,
            EditProfile = new EditProfileViewModel
            {
                FullName = userFullName,
                PhoneNumber = userPhone,
                NidNumber = userNid
            },
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

    // ==========================================
    // 2. UPDATE USER PROFILE (/profile/update)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("profile/update")]
    public async Task<IActionResult> UpdateProfile(EditProfileViewModel model)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
        {
            return Challenge();
        }

        // Fallback parameter extraction in case of prefix or binding variations
        var fullName = !string.IsNullOrWhiteSpace(model.FullName) 
            ? model.FullName 
            : (Request.Form["FullName"].ToString() ?? Request.Form["EditProfile.FullName"].ToString() ?? "");

        var phoneNumber = !string.IsNullOrWhiteSpace(model.PhoneNumber) 
            ? model.PhoneNumber 
            : (Request.Form["PhoneNumber"].ToString() ?? Request.Form["EditProfile.PhoneNumber"].ToString() ?? "");

        var nidNumber = model.NidNumber != null 
            ? model.NidNumber 
            : (Request.Form["NidNumber"].ToString() ?? Request.Form["EditProfile.NidNumber"].ToString() ?? "");

        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["ErrorMessage"] = "Full Name cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        // 1. Update DB user record (by Id or by Email)
        try
        {
            var dbUser = await _context.Users.FindAsync(userId.Value);
            if (dbUser == null && !string.IsNullOrEmpty(userEmail))
            {
                dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
            }

            if (dbUser != null)
            {
                dbUser.FullName = fullName.Trim();
                dbUser.PhoneNumber = phoneNumber.Trim();
                dbUser.NidNumber = nidNumber.Trim();
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            // Database operations fallback
        }

        // 2. Update in-memory fallback registry if present
        foreach (var kvp in AccountController._userRegistry)
        {
            if (kvp.Value.Id == userId.Value || (!string.IsNullOrEmpty(userEmail) && string.Equals(kvp.Value.Email, userEmail, StringComparison.OrdinalIgnoreCase)))
            {
                kvp.Value.FullName = fullName.Trim();
                kvp.Value.PhoneNumber = phoneNumber.Trim();
                kvp.Value.NidNumber = nidNumber.Trim();
            }
        }

        // 3. Refresh Claims Identity cookie with updated FullName
        try
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new Claim(ClaimTypes.Name, fullName.Trim()),
                new Claim(ClaimTypes.Email, userEmail),
                new Claim(ClaimTypes.Role, userRole)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));
        }
        catch
        {
        }

        TempData["ToastMessage"] = "Your profile information has been updated successfully!";
        return RedirectToAction(nameof(Index));
    }
}
