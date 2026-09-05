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
            .Include(p => p.BiddingRequests)
            .Include(p => p.Auctions)
                .ThenInclude(a => a.Bids)
            .Where(p => p.SellerId == userId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var sellingHistory = userProperties.Select(p => {
            var coverImg = p.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
            var activeAuction = p.Auctions.FirstOrDefault(a => a.Status == AuctionStatus.Active && a.EndTime > now);
            var latestAuction = p.Auctions.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            var highestBid = activeAuction?.Bids.OrderByDescending(b => b.Amount).FirstOrDefault()?.Amount;

            return new MyPropertyListingViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Price = p.Price,
                PropertyType = p.PropertyType,
                Location = $"{p.City}, {p.State}",
                Bedrooms = p.Bedrooms,
                Bathrooms = p.Bathrooms,
                SquareFeet = p.SquareFeet,
                MainImageUrl = !string.IsNullOrWhiteSpace(coverImg) 
                    ? coverImg 
                    : "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                VerificationStatus = p.VerificationStatus,
                TransactionStatus = p.TransactionStatus,
                RejectionReason = p.RejectionReason,
                CreatedAt = p.CreatedAt,
                HasActiveAuction = activeAuction != null,
                AuctionStatus = latestAuction?.Status,
                ActiveAuctionId = (activeAuction ?? latestAuction)?.Id,
                CurrentHighestBid = highestBid ?? (activeAuction != null ? activeAuction.StartPrice : null),
                HasPendingBiddingRequest = p.BiddingRequests.Any(r => r.Status == BiddingRequestStatus.Pending)
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

        // 3. Fetch authenticated user's SELLER AUCTIONS
        var sellerAuctionsEntities = await _context.Auctions
            .Include(a => a.Property)
                .ThenInclude(p => p!.Images)
            .Include(a => a.Bids)
                .ThenInclude(b => b.Buyer)
            .Include(a => a.WinningBid)
                .ThenInclude(w => w!.Buyer)
            .Where(a => a.Property != null && a.Property.SellerId == userId.Value)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var sellerAuctions = sellerAuctionsEntities.Select(a => {
            var propImg = a.Property?.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
            var highestBidEntity = a.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            var winningBidEntity = a.WinningBid ?? (a.Status == AuctionStatus.AwaitingSellerConfirmation ? highestBidEntity : null);

            return new SellerAuctionItemViewModel
            {
                AuctionId = a.Id,
                PropertyId = a.PropertyId,
                PropertyTitle = a.Property?.Title ?? "Auction Property",
                PropertyImageUrl = !string.IsNullOrWhiteSpace(propImg)
                    ? propImg
                    : "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                PropertyType = a.Property?.PropertyType ?? PropertyType.House,
                Location = a.Property != null ? $"{a.Property.City}, {a.Property.State}" : "Location",
                StartPrice = a.StartPrice,
                CurrentHighestBid = highestBidEntity?.Amount ?? a.StartPrice,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Status = a.Status,
                TotalBids = a.Bids.Count,
                WinningBidId = winningBidEntity?.Id,
                WinningBidAmount = winningBidEntity?.Amount,
                WinningBuyerName = winningBidEntity?.Buyer?.FullName ?? "Highest Bidder",
                WinningBuyerEmail = winningBidEntity?.Buyer?.Email ?? "",
                WinningBuyerPhone = winningBidEntity?.Buyer?.PhoneNumber ?? "",
                SellerDecisionAt = a.SellerDecisionAt,
                SellerDecisionNotes = a.SellerDecisionNotes
            };
        }).ToList();

        // 4. Fetch authenticated user's BUYER BIDS
        var userBids = await _context.Bids
            .Include(b => b.Auction)
                .ThenInclude(a => a!.Property)
                    .ThenInclude(p => p!.Images)
            .Include(b => b.Auction)
                .ThenInclude(a => a!.Bids)
            .Include(b => b.Auction)
                .ThenInclude(a => a!.WinningBid)
            .Where(b => b.BuyerId == userId.Value && b.Auction != null && b.Auction.Property != null)
            .OrderByDescending(b => b.PlacedAt)
            .ToListAsync();

        // Group by Auction to present one summary row per auction with the user's latest bid
        var buyerBids = userBids
            .GroupBy(b => b.AuctionId)
            .Select(g => {
                var latestBid = g.OrderByDescending(b => b.PlacedAt).First();
                var auction = latestBid.Auction!;
                var property = auction.Property!;
                var propImg = property.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
                var allAuctionBids = auction.Bids.OrderByDescending(b => b.Amount).ToList();
                var currentHighestBid = allAuctionBids.FirstOrDefault()?.Amount ?? auction.StartPrice;
                var highestBidderId = allAuctionBids.FirstOrDefault()?.BuyerId;

                string position;
                if (auction.Status == AuctionStatus.Active && DateTime.UtcNow < auction.EndTime)
                {
                    position = (highestBidderId == userId.Value) ? "Winning" : "Outbid";
                }
                else if (auction.Status == AuctionStatus.Sold || auction.Status == AuctionStatus.AwaitingSellerConfirmation)
                {
                    var winnerId = auction.WinningBid?.BuyerId ?? highestBidderId;
                    position = (winnerId == userId.Value) ? "Won" : "Lost";
                }
                else
                {
                    position = "Lost";
                }

                return new BuyerBidItemViewModel
                {
                    BidId = latestBid.Id,
                    AuctionId = auction.Id,
                    PropertyId = property.Id,
                    PropertyTitle = property.Title,
                    PropertyImageUrl = !string.IsNullOrWhiteSpace(propImg)
                        ? propImg
                        : "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                    Location = $"{property.City}, {property.State}",
                    YourLatestBid = latestBid.Amount,
                    CurrentHighestBid = currentHighestBid,
                    PlacedAt = latestBid.PlacedAt,
                    EndTime = auction.EndTime,
                    AuctionStatus = auction.Status,
                    BidPosition = position
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
            ActiveAuctionsCount = sellerAuctions.Count(a => a.Status == AuctionStatus.Active || a.Status == AuctionStatus.AwaitingSellerConfirmation),
            MyBidsCount = buyerBids.Count,
            SellingHistory = sellingHistory,
            BuyingHistory = buyingHistory,
            SellerAuctions = sellerAuctions,
            BuyerBids = buyerBids
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

    // ==========================================
    // 3. CONFIRM WINNING BID (CREATES TRANSACTION)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("profile/auctions/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmAuctionSale(Guid id)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Challenge();

        var auction = await _context.Auctions
            .Include(a => a.Property)
            .Include(a => a.Bids)
                .ThenInclude(b => b.Buyer)
            .Include(a => a.WinningBid)
                .ThenInclude(w => w!.Buyer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null || auction.Property == null)
        {
            TempData["ErrorMessage"] = "Auction record not found.";
            return RedirectToAction(nameof(Index));
        }

        if (auction.Property.SellerId != userId.Value && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "You can only confirm sales for properties you own.";
            return RedirectToAction(nameof(Index));
        }

        if (auction.Status != AuctionStatus.AwaitingSellerConfirmation)
        {
            TempData["ErrorMessage"] = "This auction is not currently awaiting seller confirmation.";
            return RedirectToAction(nameof(Index));
        }

        var winningBid = auction.WinningBid ?? auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (winningBid == null)
        {
            TempData["ErrorMessage"] = "No winning bid found for this auction.";
            return RedirectToAction(nameof(Index));
        }

        // 1. Mark auction as Sold
        auction.Status = AuctionStatus.Sold;
        auction.WinningBidId = winningBid.Id;
        auction.SellerDecisionAt = DateTime.UtcNow;
        auction.SellerDecisionNotes = $"Confirmed by Seller on {DateTime.UtcNow:g}. Agreed price: ${winningBid.Amount:N0}.";

        // 2. Mark Property Transaction Status
        auction.Property.TransactionStatus = TransactionStatus.AgreementReached;

        // 3. Create real PropertyTransaction agreement
        var transaction = new PropertyTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = auction.PropertyId,
            BuyerId = winningBid.BuyerId,
            AgreedPrice = winningBid.Amount,
            Status = TransactionStatus.AgreementReached,
            Notes = $"Property successfully sold via PropLink Live Auction (Auction ID: {auction.Id:N}). Confirmed winning bid: ${winningBid.Amount:N0}.",
            TransactionDate = DateTime.UtcNow
        };

        _context.PropertyTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        TempData["SubmissionSuccess"] = $"Congratulations! You have confirmed the winning bid of ${winningBid.Amount:N0} from {winningBid.Buyer?.FullName ?? "Buyer"}. A formal Purchase Agreement has been recorded!";
        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 4. REJECT WINNING BID (REOPENS PROPERTY)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("profile/auctions/{id:guid}/reject")]
    public async Task<IActionResult> RejectAuctionWinningBid(Guid id, string? rejectionNotes)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Challenge();

        var auction = await _context.Auctions
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null || auction.Property == null)
        {
            TempData["ErrorMessage"] = "Auction record not found.";
            return RedirectToAction(nameof(Index));
        }

        if (auction.Property.SellerId != userId.Value && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "You can only manage auctions for properties you own.";
            return RedirectToAction(nameof(Index));
        }

        if (auction.Status != AuctionStatus.AwaitingSellerConfirmation)
        {
            TempData["ErrorMessage"] = "This auction is not currently awaiting seller confirmation.";
            return RedirectToAction(nameof(Index));
        }

        // Mark auction as Cancelled/Unsold
        auction.Status = AuctionStatus.Cancelled;
        auction.SellerDecisionAt = DateTime.UtcNow;
        auction.SellerDecisionNotes = !string.IsNullOrWhiteSpace(rejectionNotes)
            ? rejectionNotes.Trim()
            : "Seller declined winning bid. Property reverted to standard verified catalog.";

        // Property returns to normal available state
        auction.Property.TransactionStatus = TransactionStatus.Available;

        await _context.SaveChangesAsync();

        TempData["ToastMessage"] = "The auction has been closed as unsold. Your listing remains active in the verified marketplace for direct offers or future bidding requests.";
        return RedirectToAction(nameof(Index));
    }
}
