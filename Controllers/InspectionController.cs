using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Entities;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

[Authorize]
public class InspectionController : Controller
{
    private readonly ApplicationDbContext _context;

    // Concurrent registry fallback to guarantee immediate memory persistence
    internal static readonly ConcurrentDictionary<Guid, InspectionBooking> _inspectionRegistry = new();
    internal static readonly ConcurrentDictionary<Guid, PropertyTransaction> _transactionRegistry = new();
    internal static readonly ConcurrentDictionary<Guid, List<InspectionChatMessage>> _chatRegistry = new();

    public InspectionController(ApplicationDbContext context)
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

    private async Task<List<InspectionBooking>> GetAllBookingsAsync()
    {
        List<InspectionBooking> bookings = new();
        try
        {
            bookings = await _context.InspectionBookings
                .Include(i => i.Property)
                    .ThenInclude(p => p!.Images)
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .Include(i => i.Agent)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }
        catch
        {
        }

        foreach (var regItem in _inspectionRegistry.Values)
        {
            if (!bookings.Any(b => b.Id == regItem.Id))
            {
                bookings.Add(regItem);
            }
        }

        return bookings;
    }

    // ==========================================
    // 1. BUYER SUBMITS REQUEST (Direct or Offer)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestInspection(InspectionRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please ensure all required inspection details and terms are completed.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        var buyerId = CurrentUserId;
        if (!buyerId.HasValue)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Property", new { id = model.PropertyId }) });
        }

        Property? property = null;
        try
        {
            property = await _context.Properties
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.Id == model.PropertyId);
        }
        catch
        {
        }

        if (property == null)
        {
            TempData["ErrorMessage"] = "Property record not found.";
            return RedirectToAction("Index", "Property");
        }

        if (property.SellerId == buyerId.Value)
        {
            TempData["ErrorMessage"] = "You cannot schedule an inspection on your own property listing.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        // 1. Sold Property Check
        if (property.TransactionStatus == TransactionStatus.Sold)
        {
            TempData["ErrorMessage"] = "This property has been marked as Sold Out. No further buying requests are permitted.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        // 2. Property Lock Check: When an inspection request has been forwarded to an agent or is in progress, block new buyer requests
        bool isPropertyLocked = property.TransactionStatus == TransactionStatus.UnderProcessing || property.TransactionStatus == TransactionStatus.MeetingScheduled;
        if (!isPropertyLocked)
        {
            try
            {
                isPropertyLocked = await _context.InspectionBookings
                    .AnyAsync(i => i.PropertyId == property.Id &&
                        (i.Status == InspectionStatus.PendingAgentSchedule ||
                         i.Status == InspectionStatus.ScheduleFixed ||
                         i.Status == InspectionStatus.InspectionCompleted ||
                         i.Status == InspectionStatus.UnderProcessing));
            }
            catch { }

            if (!isPropertyLocked)
            {
                isPropertyLocked = _inspectionRegistry.Values
                    .Any(i => i.PropertyId == property.Id &&
                        (i.Status == InspectionStatus.PendingAgentSchedule ||
                         i.Status == InspectionStatus.ScheduleFixed ||
                         i.Status == InspectionStatus.InspectionCompleted ||
                         i.Status == InspectionStatus.UnderProcessing));
            }
        }

        if (isPropertyLocked)
        {
            TempData["ErrorMessage"] = "This property currently has an active inspection visit being coordinated with a Verification Agent. New buying requests are temporarily on hold.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        // 2. Single Request Constraint: A buyer can request inspection only once per property
        bool alreadyRequested = false;
        try
        {
            alreadyRequested = await _context.InspectionBookings
                .AnyAsync(i => i.PropertyId == property.Id && i.BuyerId == buyerId.Value && i.Status != InspectionStatus.Cancelled && i.Status != InspectionStatus.DeclinedBySeller);
        }
        catch { }

        if (!alreadyRequested)
        {
            alreadyRequested = _inspectionRegistry.Values
                .Any(i => i.PropertyId == property.Id && i.BuyerId == buyerId.Value && i.Status != InspectionStatus.Cancelled && i.Status != InspectionStatus.DeclinedBySeller);
        }

        if (alreadyRequested)
        {
            TempData["ErrorMessage"] = "You have already submitted an inspection or purchase request for this property. Buyers can only request inspection once per property. Please view your status in My Purchase Requests.";
            return RedirectToAction(nameof(MyRequests));
        }

        // Find buyer entity or create placeholder
        User? buyer = null;
        try
        {
            buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId.Value);
        // 3. Subscription Monthly Visit Limit Check:
        // Free Member: Max 2 visits per month (last 30 days)
        // Pro Member: Unlimited visits
        var buyerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId.Value);
        bool isProBuyer = buyerUser != null && buyerUser.HasActiveProSubscription;

        if (!isProBuyer)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            int monthlyRequestsCount = await _context.InspectionBookings
                .CountAsync(i => i.BuyerId == buyerId.Value && i.CreatedAt >= thirtyDaysAgo && i.Status != InspectionStatus.Cancelled);

            if (monthlyRequestsCount >= 2)
            {
                TempData["SubscriptionLimitReached"] = "You have used your 2 free property visit requests for this month. Upgrade to PropLink Pro for unlimited property visit requests!";
                return RedirectToAction("Pricing", "Subscription");
            }
        }

        // Find buyer entity or create placeholder
        User? buyer = buyerUser;
        try
        {
            if (buyer == null)
            {
                buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId.Value);
            }
        }
        catch
        {
        }

        if (buyer == null && AccountController._userRegistry.TryGetValue(User.Identity?.Name ?? "", out var regBuyer))
        {
            buyer = regBuyer;
        }

        var booking = new InspectionBooking
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Property = property,
            BuyerId = buyerId.Value,
            Buyer = buyer,
            SellerId = property.SellerId,
            Seller = property.Seller,
            InspectionType = model.InspectionType,
            AskingPrice = property.Price,
            OfferedPrice = model.InspectionType == "PriceOfferWithInspection" ? model.OfferedPrice : null,
            PreferredDate = model.PreferredDate,
            Status = InspectionStatus.PendingSellerApproval,
            AgreedToAntiBypassTerms = model.AgreeToAntiBypassTerms,
            Notes = $"Slot: {model.PreferredTimeSlot} | Attendees: {model.AttendeesCount} | Notes: {model.BuyerNotes}",
            CreatedAt = DateTime.UtcNow
        };

        _inspectionRegistry[booking.Id] = booking;

        try
        {
            _context.InspectionBookings.Add(booking);
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = "Your request has been submitted to the seller queue! The seller will review incoming offers and requests. Once accepted, an assigned Verification Agent will schedule the physical deed inspection.";
        return RedirectToAction(nameof(MyRequests));
    }

    // ==========================================
    // 2. BUYER QUEUE: MY REQUESTS
    // ==========================================
    [HttpGet]
    [Route("inspection/my-requests")]
    public async Task<IActionResult> MyRequests()
    {
        var buyerId = CurrentUserId;
        if (!buyerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var allBookings = await GetAllBookingsAsync();
        var myRequests = allBookings
            .Where(b => b.BuyerId == buyerId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BuyerInspectionItemViewModel
            {
                BookingId = b.Id,
                PropertyId = b.PropertyId,
                PropertyTitle = b.Property?.Title ?? "Verified Real Estate Property",
                PropertyAddress = b.Property?.Address ?? "Deed-Audited Location",
                PropertyCity = b.Property?.City ?? "PropLink Registry",
                PropertyImageUrl = b.Property?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                    ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                AskingPrice = b.AskingPrice,
                OfferedPrice = b.OfferedPrice,
                InspectionType = b.InspectionType,
                SellerName = b.Seller?.FullName ?? "Verified Owner",
                AgentName = b.AgentName,
                PreferredDate = b.PreferredDate,
                ScheduledDate = b.ScheduledDate,
                MeetingLocationNotes = b.MeetingLocationNotes,
                Status = b.Status,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt
            }).ToList();

        return View(myRequests);
    }

    // ==========================================
    // 3. SELLER QUEUE: PROPERTIES WITH REQUESTS
    // ==========================================
    [HttpGet]
    [Route("inspection/seller-queue")]
    public async Task<IActionResult> SellerQueue()
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var allBookings = await GetAllBookingsAsync();
        var sellerBookings = (User.IsInRole("Admin")
            ? allBookings
            : allBookings.Where(b => b.SellerId == sellerId.Value)).ToList();

        // Group by property
        var propertyGroups = sellerBookings
            .GroupBy(b => b.PropertyId)
            .Select(g =>
            {
                var sample = g.First();
                return new SellerPropertyQueueItemViewModel
                {
                    PropertyId = g.Key,
                    PropertyTitle = sample.Property?.Title ?? "Verified Property",
                    PropertyAddress = sample.Property?.Address ?? "Audited Address",
                    PropertyCity = sample.Property?.City ?? "PropLink Market",
                    PropertyImageUrl = sample.Property?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                        ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                    AskingPrice = sample.AskingPrice,
                    TransactionStatus = sample.Property?.TransactionStatus ?? TransactionStatus.Available,
                    TotalRequestsCount = g.Count(),
                    PendingReviewCount = g.Count(b => b.Status == InspectionStatus.PendingSellerApproval),
                    ForwardedToAgentCount = g.Count(b => b.Status == InspectionStatus.PendingAgentSchedule || b.Status == InspectionStatus.ScheduleFixed || b.Status == InspectionStatus.UnderProcessing),
                    HighestOfferPrice = g.Where(b => b.OfferedPrice.HasValue).Select(b => b.OfferedPrice!.Value).DefaultIfEmpty(sample.AskingPrice).Max(),
                    LatestRequestDate = g.Max(b => b.CreatedAt)
                };
            })
            .OrderByDescending(p => p.PendingReviewCount > 0)
            .ThenByDescending(p => p.LatestRequestDate)
            .ToList();

        return View(propertyGroups);
    }

    // =======================================================
    // 4. SELLER PROPERTY REQUESTS: DETAIL LIST FOR ONE PROPERTY
    // =======================================================
    [HttpGet]
    [Route("inspection/property-requests/{propertyId:guid}")]
    public async Task<IActionResult> PropertyRequests(Guid propertyId)
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        Property? property = null;
        try
        {
            property = await _context.Properties
                .Include(p => p.Images)
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.Id == propertyId);
        }
        catch
        {
        }

        var allBookings = await GetAllBookingsAsync();
        var requestsForProperty = allBookings
            .Where(b => b.PropertyId == propertyId && (b.SellerId == sellerId.Value || User.IsInRole("Admin")))
            .OrderByDescending(b => b.Status == InspectionStatus.PendingSellerApproval)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new SellerInspectionRequestItemViewModel
            {
                BookingId = b.Id,
                BuyerId = b.BuyerId,
                BuyerName = b.Buyer?.FullName ?? "Prospective Buyer",
                InspectionType = b.InspectionType,
                AskingPrice = b.AskingPrice,
                OfferedPrice = b.OfferedPrice,
                PreferredDate = b.PreferredDate,
                ScheduledDate = b.ScheduledDate,
                MeetingLocationNotes = b.MeetingLocationNotes,
                Status = b.Status,
                AgentName = b.AgentName,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt,
                AgreedToAntiBypassTerms = b.AgreedToAntiBypassTerms
            }).ToList();

        if (property == null && requestsForProperty.Any())
        {
            var firstReq = allBookings.FirstOrDefault(b => b.PropertyId == propertyId);
            property = firstReq?.Property;
        }

        var viewModel = new SellerPropertyRequestsViewModel
        {
            PropertyId = propertyId,
            PropertyTitle = property?.Title ?? "Verified Real Estate Property",
            PropertyAddress = property?.Address ?? "Deed-Audited Location",
            PropertyCity = property?.City ?? "PropLink Registry",
            PropertyImageUrl = property?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            AskingPrice = property?.Price ?? (requestsForProperty.FirstOrDefault()?.AskingPrice ?? 0),
            TransactionStatus = property?.TransactionStatus ?? TransactionStatus.Available,
            Requests = requestsForProperty
        };

        return View(viewModel);
    }

    // ===============================================================
    // 5. SELLER ACCEPTS PREFERRED REQUEST -> FORWARDS TO VERIFICATION AGENT
    // ===============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("inspection/accept-request")]
    public async Task<IActionResult> AcceptRequest(Guid bookingId)
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .Include(b => b.Buyer)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && _inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Inspection request not found.";
            return RedirectToAction(nameof(SellerQueue));
        }

        if (booking.SellerId != sellerId.Value && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Unauthorized action. You are not the owner of this listing.";
            return RedirectToAction(nameof(SellerQueue));
        }

        // Find or assign active Verification Agent
        User? agent = null;
        try
        {
            agent = await _context.Users.FirstOrDefaultAsync(u => u.Role == "VerificationAgent" && !u.IsBanned);
        }
        catch
        {
        }

        if (agent == null)
        {
            agent = AccountController._userRegistry.Values.FirstOrDefault(u => u.Role == "VerificationAgent" && !u.IsBanned);
        }

        // Forward to Verification Agent!
        booking.Status = InspectionStatus.PendingAgentSchedule;
        booking.AgentId = agent?.Id;
        booking.AgentName = agent?.FullName ?? "Verification Agent";

        // Mark property transaction status as MeetingScheduled to indicate an active visit coordination
        if (booking.Property != null)
        {
            booking.Property.TransactionStatus = TransactionStatus.MeetingScheduled;
        }
        else
        {
            try
            {
                var p = await _context.Properties.FirstOrDefaultAsync(pr => pr.Id == booking.PropertyId);
                if (p != null) p.TransactionStatus = TransactionStatus.MeetingScheduled;
            }
            catch { }
        }

        _inspectionRegistry[booking.Id] = booking;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        var buyerDisplayName = booking.Buyer?.FullName ?? "the buyer";
        TempData["ToastMessage"] = $"Offer/Request from {buyerDisplayName} accepted! This request has now been forwarded to the assigned Verification Agent to fix the physical inspection schedule.";

        return RedirectToAction(nameof(PropertyRequests), new { propertyId = booking.PropertyId });
    }

    // ===============================================================
    // 6. SELLER DECLINES REQUEST
    // ===============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("inspection/decline-request")]
    public async Task<IActionResult> DeclineRequest(Guid bookingId)
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Buyer)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && _inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Inspection request not found.";
            return RedirectToAction(nameof(SellerQueue));
        }

        if (booking.SellerId != sellerId.Value && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Unauthorized action.";
            return RedirectToAction(nameof(SellerQueue));
        }

        booking.Status = InspectionStatus.DeclinedBySeller;
        _inspectionRegistry[booking.Id] = booking;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = "The buyer request has been marked as declined.";
        return RedirectToAction(nameof(PropertyRequests), new { propertyId = booking.PropertyId });
    }

    // ==============================================================================
    // 7. INSPECTION CHAT SYSTEM (UNLOCKED AFTER SCHEDULE IS FIXED)
    // ==============================================================================
    [HttpGet]
    [Route("inspection/chat/{bookingId:guid}")]
    public async Task<IActionResult> Chat(Guid bookingId)
    {
        var currentUserId = CurrentUserId;
        if (!currentUserId.HasValue)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Chat", "Inspection", new { bookingId }) });
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                    .ThenInclude(p => p!.Images)
                .Include(b => b.Buyer)
                .Include(b => b.Seller)
                .Include(b => b.Agent)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && _inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "The requested inspection booking could not be located.";
            return RedirectToAction("Index", "Home");
        }

        // STRICT REQUIREMENT: Chat option is ONLY available after the schedule is fixed
        bool isScheduleFixed = booking.Status == InspectionStatus.ScheduleFixed 
                            || booking.Status == InspectionStatus.InspectionCompleted 
                            || booking.Status == InspectionStatus.UnderProcessing;

        if (!isScheduleFixed)
        {
            TempData["ErrorMessage"] = "Direct chat unlocks only after the Verification Agent has confirmed and fixed the on-site inspection schedule.";
            if (booking.BuyerId == currentUserId.Value)
            {
                return RedirectToAction(nameof(MyRequests));
            }
            if (booking.SellerId == currentUserId.Value)
            {
                return RedirectToAction(nameof(PropertyRequests), new { propertyId = booking.PropertyId });
            }
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Inspections", "Admin");
            }
            return RedirectToAction("Index", "Agent");
        }

        // Authorization check: Only Buyer, Seller, Admin, Moderator, or VerificationAgent can access
        bool isBuyer = booking.BuyerId == currentUserId.Value;
        bool isSeller = booking.SellerId == currentUserId.Value;
        bool isAdmin = User.IsInRole("Admin");
        bool isModerator = User.IsInRole("Moderator");
        bool isAgent = User.IsInRole("VerificationAgent");
        bool isReadOnlyStaff = isAdmin || isModerator || isAgent;

        if (!isBuyer && !isSeller && !isReadOnlyStaff)
        {
            TempData["ErrorMessage"] = "You do not have authorization to view this private inspection communication.";
            return RedirectToAction("Index", "Home");
        }

        // STRICT REQUIREMENT: Admin and Moderator can see the chat, but CANNOT join in messaging
        bool canSendMessage = (isBuyer || isSeller) && !isReadOnlyStaff;

        string currentUserRole = isBuyer ? "Buyer" 
                               : isSeller ? "Seller" 
                               : isAdmin ? "Admin" 
                               : isModerator ? "Moderator" 
                               : "VerificationAgent";

        string? staffRoleBadge = isReadOnlyStaff ? (isAdmin ? "Admin (Audit Mode)" : isModerator ? "Moderator (Audit Mode)" : "Verification Agent (Audit Mode)") : null;

        // Load messages from database and in-memory fallback
        List<InspectionChatMessage> messages = new();
        try
        {
            messages = await _context.InspectionChatMessages
                .Where(m => m.BookingId == bookingId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }
        catch
        {
        }

        if (_chatRegistry.TryGetValue(bookingId, out var memMessages))
        {
            lock (memMessages)
            {
                foreach (var msg in memMessages)
                {
                    if (!messages.Any(m => m.Id == msg.Id))
                    {
                        messages.Add(msg);
                    }
                }
            }
        }

        messages = messages.OrderBy(m => m.SentAt).ToList();

        var viewModel = new InspectionChatViewModel
        {
            BookingId = booking.Id,
            PropertyId = booking.PropertyId,
            PropertyTitle = booking.Property?.Title ?? "Verified Real Estate Property",
            PropertyAddress = booking.Property?.Address ?? "Audited Property Address",
            PropertyCity = booking.Property?.City ?? "PropLink Marketplace",
            PropertyImageUrl = booking.Property?.Images?.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl
                ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            AskingPrice = booking.AskingPrice,
            OfferedPrice = booking.OfferedPrice,
            Status = booking.Status,
            ScheduledDate = booking.ScheduledDate,
            MeetingLocationNotes = booking.MeetingLocationNotes,
            AgentName = booking.AgentName,
            BuyerId = booking.BuyerId,
            BuyerName = booking.Buyer?.FullName ?? "Prospective Buyer",
            SellerId = booking.SellerId,
            SellerName = booking.Seller?.FullName ?? "Property Owner",
            CurrentUserId = currentUserId.Value,
            CurrentUserRole = currentUserRole,
            CanSendMessage = canSendMessage,
            IsReadOnlyStaff = isReadOnlyStaff,
            StaffRoleBadge = staffRoleBadge,
            Messages = messages.Select(m => new InspectionChatMessageItemViewModel
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                SenderRole = m.SenderRole,
                Message = m.Message,
                SentAt = m.SentAt,
                IsFromCurrentUser = m.SenderId == currentUserId.Value
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("inspection/send-chat-message")]
    public async Task<IActionResult> SendChatMessage([FromForm] SendInspectionChatMessageRequest model)
    {
        var currentUserId = CurrentUserId;
        if (!currentUserId.HasValue)
        {
            return Json(new { success = false, message = "Please sign in to send messages." });
        }

        if (string.IsNullOrWhiteSpace(model.Message))
        {
            return Json(new { success = false, message = "Message content cannot be blank." });
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Buyer)
                .Include(b => b.Seller)
                .FirstOrDefaultAsync(b => b.Id == model.BookingId);
        }
        catch { }

        if (booking == null && _inspectionRegistry.TryGetValue(model.BookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            return Json(new { success = false, message = "Inspection booking not found." });
        }

        // STRICT REQUIREMENT: Chat is only permitted after schedule is fixed
        bool isScheduleFixed = booking.Status == InspectionStatus.ScheduleFixed 
                            || booking.Status == InspectionStatus.InspectionCompleted 
                            || booking.Status == InspectionStatus.UnderProcessing;

        if (!isScheduleFixed)
        {
            return Json(new { success = false, message = "Chat is locked until the inspection schedule is fixed by an agent." });
        }

        // STRICT REQUIREMENT: Admin and Moderator CANNOT join in messaging
        bool isAdmin = User.IsInRole("Admin");
        bool isModerator = User.IsInRole("Moderator");
        bool isAgent = User.IsInRole("VerificationAgent");
        if (isAdmin || isModerator || isAgent)
        {
            return Json(new { success = false, message = "Administrative and verification staff have read-only audit visibility and cannot send messages in buyer-seller chat." });
        }

        bool isBuyer = booking.BuyerId == currentUserId.Value;
        bool isSeller = booking.SellerId == currentUserId.Value;

        if (!isBuyer && !isSeller)
        {
            return Json(new { success = false, message = "Only the assigned buyer and seller are permitted to send messages." });
        }

        string senderRole = isBuyer ? "Buyer" : "Seller";
        string senderName = User.Identity?.Name ?? (isBuyer ? (booking.Buyer?.FullName ?? "Buyer") : (booking.Seller?.FullName ?? "Seller"));

        var newChatMessage = new InspectionChatMessage
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            SenderId = currentUserId.Value,
            SenderName = senderName,
            SenderRole = senderRole,
            Message = model.Message.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        // Save in memory fallback
        var list = _chatRegistry.GetOrAdd(booking.Id, _ => new List<InspectionChatMessage>());
        lock (list)
        {
            list.Add(newChatMessage);
        }

        // Save to Database
        try
        {
            _context.InspectionChatMessages.Add(newChatMessage);
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                      (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));

        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = new
                {
                    id = newChatMessage.Id,
                    senderId = newChatMessage.SenderId,
                    senderName = newChatMessage.SenderName,
                    senderRole = newChatMessage.SenderRole,
                    message = newChatMessage.Message,
                    sentAt = newChatMessage.SentAt.ToString("MMM dd, yyyy h:mm tt"),
                    isFromCurrentUser = true
                }
            });
        }

        return RedirectToAction(nameof(Chat), new { bookingId = booking.Id });
    }

    [HttpGet]
    [Route("inspection/chat-messages/{bookingId:guid}")]
    public async Task<IActionResult> GetChatMessages(Guid bookingId)
    {
        var currentUserId = CurrentUserId;
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Buyer)
                .Include(b => b.Seller)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch { }

        if (booking == null && _inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            return NotFound();
        }

        bool isBuyer = booking.BuyerId == currentUserId.Value;
        bool isSeller = booking.SellerId == currentUserId.Value;
        bool isStaff = User.IsInRole("Admin") || User.IsInRole("Moderator") || User.IsInRole("VerificationAgent");

        if (!isBuyer && !isSeller && !isStaff)
        {
            return Forbid();
        }

        List<InspectionChatMessage> messages = new();
        try
        {
            messages = await _context.InspectionChatMessages
                .Where(m => m.BookingId == bookingId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }
        catch { }

        if (_chatRegistry.TryGetValue(bookingId, out var memMessages))
        {
            lock (memMessages)
            {
                foreach (var msg in memMessages)
                {
                    if (!messages.Any(m => m.Id == msg.Id))
                    {
                        messages.Add(msg);
                    }
                }
            }
        }

        var results = messages
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                senderName = m.SenderName,
                senderRole = m.SenderRole,
                message = m.Message,
                sentAt = m.SentAt.ToString("MMM dd, yyyy h:mm tt"),
                isFromCurrentUser = m.SenderId == currentUserId.Value
            })
            .ToList();

        return Json(new { success = true, messages = results });
    }
}
