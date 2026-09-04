using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Application.Common.Interfaces;
using PropLink.Domain.Entities;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

public class PropertyController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<PropertyController> _logger;

    public PropertyController(
        ApplicationDbContext context,
        ICloudStorageService cloudStorageService,
        ILogger<PropertyController> logger)
    {
        _context = context;
        _cloudStorageService = cloudStorageService;
        _logger = logger;
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
    // 1. PUBLIC PROPERTIES CATALOG (/properties)
    // ==========================================
    [HttpGet]
    [Route("properties")]
    public async Task<IActionResult> Index(
        string? searchQuery,
        PropertyType? propertyType,
        decimal? minPrice,
        decimal? maxPrice,
        string? city,
        string? sortBy,
        int page = 1,
        int pageSize = 6)
    {
        // Enforce backend/database query filter: ONLY APPROVED PROPERTIES
        var query = _context.Properties
            .Include(p => p.Images)
            .Include(p => p.Seller)
            .Where(p => p.VerificationStatus == VerificationStatus.Approved)
            .AsNoTracking();

        // Apply Search filters
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var cleanQuery = searchQuery.Trim().ToLower();
            query = query.Where(p => 
                p.Title.ToLower().Contains(cleanQuery) || 
                p.City.ToLower().Contains(cleanQuery) || 
                p.Address.ToLower().Contains(cleanQuery) ||
                p.Description.ToLower().Contains(cleanQuery));
        }

        if (propertyType.HasValue)
        {
            query = query.Where(p => p.PropertyType == propertyType.Value);
        }

        if (minPrice.HasValue && minPrice.Value > 0)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue && maxPrice.Value > 0)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(p => p.City.ToLower() == city.Trim().ToLower());
        }

        // Sorting
        query = sortBy switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "oldest" => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        // Total count for backend pagination
        var totalItems = await query.CountAsync();

        // Database Pagination
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 3, 24);

        var pagedList = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var propertyCards = pagedList.Select(p => new PropertyCardViewModel
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            Price = p.Price,
            PropertyType = p.PropertyType,
            Address = p.Address,
            City = p.City,
            State = p.State,
            Bedrooms = p.Bedrooms,
            Bathrooms = p.Bathrooms,
            SquareFeet = p.SquareFeet,
            ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl
                       ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            VerificationStatus = p.VerificationStatus,
            TransactionStatus = p.TransactionStatus,
            SellerName = p.Seller?.FullName ?? "Verified Seller",
            TimeAgo = GetTimeAgo(p.CreatedAt)
        }).ToList();

        var viewModel = new PropertiesIndexViewModel
        {
            Properties = propertyCards,
            SearchQuery = searchQuery,
            PropertyType = propertyType,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            City = city,
            SortBy = sortBy,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(viewModel);
    }

    // ==========================================
    // 2. PUBLIC PROPERTY DETAILS (/properties/{id})
    // ==========================================
    [HttpGet]
    [Route("properties/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var property = await _context.Properties
            .Include(p => p.Images)
            .Include(p => p.Seller)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
        {
            return NotFound();
        }

        var isOwner = CurrentUserId.HasValue && property.SellerId == CurrentUserId.Value;
        var isAdmin = User.IsInRole("Admin");

        // SECURITY CHECK: Unapproved property must NOT be accessible publicly
        if (property.VerificationStatus != VerificationStatus.Approved && !isOwner && !isAdmin)
        {
            return NotFound();
        }

        var sellerTotalProperties = await _context.Properties
            .CountAsync(p => p.SellerId == property.SellerId && p.VerificationStatus == VerificationStatus.Approved);

        var viewModel = new PropertyDetailViewModel
        {
            Id = property.Id,
            Title = property.Title,
            Description = property.Description,
            Price = property.Price,
            PropertyType = property.PropertyType,
            Address = property.Address,
            City = property.City,
            State = property.State,
            ZipCode = property.ZipCode,
            Bedrooms = property.Bedrooms,
            Bathrooms = property.Bathrooms,
            SquareFeet = property.SquareFeet,
            VerificationStatus = property.VerificationStatus,
            TransactionStatus = property.TransactionStatus,
            CreatedAt = property.CreatedAt,
            ImageUrls = property.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
            SellerId = property.SellerId,
            SellerName = property.Seller?.FullName ?? "Verified Seller",
            SellerEmail = property.Seller?.Email ?? "",
            SellerPhone = property.Seller?.PhoneNumber ?? "",
            SellerMemberSince = property.Seller?.CreatedAt ?? DateTime.UtcNow,
            SellerTotalProperties = sellerTotalProperties,
            IsOwner = isOwner
        };

        if (!viewModel.ImageUrls.Any())
        {
            viewModel.ImageUrls.Add("https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80");
        }

        return View(viewModel);
    }

    // ==========================================
    // ==========================================
    // 3. SELL PROPERTY (/sell-property)
    // ==========================================
    [HttpGet]
    [Authorize]
    [Route("sell-property")]
    public IActionResult Sell()
    {
        return View(new SellPropertyViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("sell-property")]
    public async Task<IActionResult> Sell(SellPropertyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var userId = CurrentUserId;
            User? user = null;

            if (userId.HasValue)
            {
                user = await _context.Users.FindAsync(userId.Value);
            }

            if (user == null)
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "user@proplink.com";
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
                
                if (user == null)
                {
                    user = new User
                    {
                        Id = userId ?? Guid.NewGuid(),
                        FullName = User.Identity?.Name ?? "Verified Seller",
                        Email = userEmail,
                        Role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
            }

            var sellerId = user.Id;

            // Create new Property entity with status PENDING
            var property = new Property
            {
                Id = Guid.NewGuid(),
                Title = model.Title.Trim(),
                Description = model.Description.Trim(),
                Price = model.Price,
                PropertyType = model.PropertyType,
                SquareFeet = model.SquareFeet,
                Bedrooms = model.Bedrooms,
                Bathrooms = model.Bathrooms,
                Address = model.Address.Trim(),
                City = model.City.Trim(),
                State = model.State.Trim(),
                ZipCode = model.ZipCode.Trim(),
                SellerId = sellerId,
                VerificationStatus = VerificationStatus.Pending,
                TransactionStatus = TransactionStatus.Available,
                ListingStatus = ListingStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            // 1. Save Gallery Images directly to Supabase Database (bytea)
            int order = 1;
            if (model.Images != null && model.Images.Any(f => f.Length > 0))
            {
                foreach (var imgFile in model.Images.Where(f => f.Length > 0))
                {
                    using var memoryStream = new MemoryStream();
                    await imgFile.CopyToAsync(memoryStream);
                    var fileBytes = memoryStream.ToArray();
                    var imageId = Guid.NewGuid();

                    property.Images.Add(new PropertyImage
                    {
                        Id = imageId,
                        PropertyId = property.Id,
                        FileData = fileBytes,
                        ContentType = string.IsNullOrWhiteSpace(imgFile.ContentType) ? "image/jpeg" : imgFile.ContentType,
                        ImageUrl = $"/storage/images/{imageId}",
                        Caption = Path.GetFileNameWithoutExtension(imgFile.FileName),
                        IsPrimary = (order == 1),
                        DisplayOrder = order++,
                        UploadedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Default architectural image if no gallery photo provided
                property.Images.Add(new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    ImageUrl = "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                    Caption = "Property Exterior",
                    IsPrimary = true,
                    DisplayOrder = 1,
                    UploadedAt = DateTime.UtcNow
                });
            }

            // 2. Process Seller NID Document directly to Supabase Database (bytea)
            if (model.NidDocument != null && model.NidDocument.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await model.NidDocument.CopyToAsync(memoryStream);
                var nidBytes = memoryStream.ToArray();
                var nidDocId = Guid.NewGuid();

                property.Documents.Add(new PropertyDocument
                {
                    Id = nidDocId,
                    PropertyId = property.Id,
                    DocumentType = "NID",
                    FileName = model.NidDocument.FileName,
                    FileData = nidBytes,
                    StorageReference = $"db://documents/{nidDocId}",
                    FilePath = $"db://documents/{nidDocId}",
                    ContentType = string.IsNullOrWhiteSpace(model.NidDocument.ContentType) ? "application/pdf" : model.NidDocument.ContentType,
                    FileSizeBytes = model.NidDocument.Length,
                    Status = VerificationStatus.Pending,
                    UploadedAt = DateTime.UtcNow
                });
            }
            else
            {
                property.Documents.Add(new PropertyDocument
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    DocumentType = "NID",
                    FileName = "seller_identity_document.pdf",
                    StorageReference = "proplink-documents-secure/nid_documents/sample_seller_nid.pdf",
                    FilePath = "proplink-documents-secure/nid_documents/sample_seller_nid.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 1048576,
                    Status = VerificationStatus.Pending,
                    UploadedAt = DateTime.UtcNow
                });
            }

            // 3. Process Property Deed / Title Document directly to Supabase Database (bytea)
            if (model.PropertyDocument != null && model.PropertyDocument.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await model.PropertyDocument.CopyToAsync(memoryStream);
                var docBytes = memoryStream.ToArray();
                var deedDocId = Guid.NewGuid();

                property.Documents.Add(new PropertyDocument
                {
                    Id = deedDocId,
                    PropertyId = property.Id,
                    DocumentType = "Deed / Title",
                    FileName = model.PropertyDocument.FileName,
                    FileData = docBytes,
                    StorageReference = $"db://documents/{deedDocId}",
                    FilePath = $"db://documents/{deedDocId}",
                    ContentType = string.IsNullOrWhiteSpace(model.PropertyDocument.ContentType) ? "application/pdf" : model.PropertyDocument.ContentType,
                    FileSizeBytes = model.PropertyDocument.Length,
                    Status = VerificationStatus.Pending,
                    UploadedAt = DateTime.UtcNow
                });
            }
            else
            {
                property.Documents.Add(new PropertyDocument
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    DocumentType = "Deed / Title",
                    FileName = "municipal_title_deed.pdf",
                    StorageReference = "proplink-documents-secure/ownership_deeds/sample_title_deed.pdf",
                    FilePath = "proplink-documents-secure/ownership_deeds/sample_title_deed.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 2097152,
                    Status = VerificationStatus.Pending,
                    UploadedAt = DateTime.UtcNow
                });
            }

            _context.Properties.Add(property);
            await _context.SaveChangesAsync();

            TempData["SubmissionSuccess"] = "Your property has been submitted and is waiting for Admin verification.";
            return RedirectToAction("Index", "Profile");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting property listing for verification");
            ModelState.AddModelError(string.Empty, "An error occurred while saving your property submission. Please try again.");
            return View(model);
        }
    }

    // ==========================================
    // PUBLIC IMAGE PROXY (Direct from Database)
    // ==========================================
    [HttpGet]
    [Route("storage/images/{id}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetPublicImage(string id)
    {
        // 1. Try finding by Primary Key GUID in Database
        if (Guid.TryParse(id, out var imageGuid))
        {
            var dbImage = await _context.PropertyImages.FindAsync(imageGuid);
            if (dbImage?.FileData != null && dbImage.FileData.Length > 0)
            {
                return File(dbImage.FileData, dbImage.ContentType ?? "image/jpeg");
            }
        }

        // 2. Try finding by ImageUrl or Caption in Database
        var matchedImage = await _context.PropertyImages
            .FirstOrDefaultAsync(i => i.ImageUrl.Contains(id) && i.FileData != null && i.FileData.Length > 0);
        if (matchedImage?.FileData != null)
        {
            return File(matchedImage.FileData, matchedImage.ContentType ?? "image/jpeg");
        }

        // 3. Fallback to Cloud Storage Service if external
        var doc = await _cloudStorageService.GetPrivateDocumentAsync(id);
        if (doc == null)
        {
            doc = await _cloudStorageService.GetPrivateDocumentAsync($"proplink-images/properties/{id}");
        }
        if (doc != null)
        {
            return File(doc.Value.FileBytes, doc.Value.ContentType, doc.Value.FileName);
        }

        return Redirect("https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80");
    }

    // ==========================================
    // 4. EDIT & RESUBMIT REJECTED PROPERTY
    // ==========================================
    [HttpGet]
    [Authorize]
    [Route("my-properties/edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Challenge();

        var property = await _context.Properties
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.SellerId == userId.Value);

        if (property == null)
        {
            return NotFound();
        }

        var model = new EditPropertyViewModel
        {
            Id = property.Id,
            Title = property.Title,
            Description = property.Description,
            Price = property.Price,
            PropertyType = property.PropertyType,
            SquareFeet = property.SquareFeet,
            Bedrooms = property.Bedrooms,
            Bathrooms = property.Bathrooms,
            Address = property.Address,
            City = property.City,
            State = property.State,
            ZipCode = property.ZipCode,
            VerificationStatus = property.VerificationStatus,
            TransactionStatus = property.TransactionStatus,
            RejectionReason = property.RejectionReason,
            ExistingImageUrls = property.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("my-properties/edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, EditPropertyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = CurrentUserId;
        if (!userId.HasValue) return Challenge();

        var property = await _context.Properties
            .Include(p => p.Images)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == id && p.SellerId == userId.Value);

        if (property == null)
        {
            return NotFound();
        }

        // Update basic specs
        property.Title = model.Title.Trim();
        property.Description = model.Description.Trim();
        property.Price = model.Price;
        property.PropertyType = model.PropertyType;
        property.SquareFeet = model.SquareFeet;
        property.Bedrooms = model.Bedrooms;
        property.Bathrooms = model.Bathrooms;
        property.Address = model.Address.Trim();
        property.City = model.City.Trim();
        property.State = model.State.Trim();
        property.ZipCode = model.ZipCode.Trim();
        property.UpdatedAt = DateTime.UtcNow;

        // Resubmission rule: REJECTED -> PENDING, reset rejection reason
        property.VerificationStatus = VerificationStatus.Pending;
        property.ListingStatus = ListingStatus.Draft;
        property.RejectionReason = null;
        property.AdminReviewNotes = "Resubmitted by seller with revisions on " + DateTime.UtcNow.ToString("g");

        // Optional: Upload additional images directly to Supabase Database (bytea)
        if (model.NewImages != null && model.NewImages.Any())
        {
            int order = property.Images.Count + 1;
            foreach (var imgFile in model.NewImages.Where(f => f.Length > 0))
            {
                using var memoryStream = new MemoryStream();
                await imgFile.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();
                var imageId = Guid.NewGuid();

                property.Images.Add(new PropertyImage
                {
                    Id = imageId,
                    PropertyId = property.Id,
                    FileData = fileBytes,
                    ContentType = string.IsNullOrWhiteSpace(imgFile.ContentType) ? "image/jpeg" : imgFile.ContentType,
                    ImageUrl = $"/storage/images/{imageId}",
                    Caption = Path.GetFileNameWithoutExtension(imgFile.FileName),
                    IsPrimary = false,
                    DisplayOrder = order++,
                    UploadedAt = DateTime.UtcNow
                });
            }
        }

        // Optional: Replace NID Document directly in Supabase Database (bytea)
        if (model.NewNidDocument != null && model.NewNidDocument.Length > 0)
        {
            using var memoryStream = new MemoryStream();
            await model.NewNidDocument.CopyToAsync(memoryStream);
            var nidBytes = memoryStream.ToArray();

            var existingNid = property.Documents.FirstOrDefault(d => d.DocumentType == "NID");
            if (existingNid != null)
            {
                existingNid.FileName = model.NewNidDocument.FileName;
                existingNid.FileData = nidBytes;
                existingNid.StorageReference = $"db://documents/{existingNid.Id}";
                existingNid.FilePath = $"db://documents/{existingNid.Id}";
                existingNid.ContentType = string.IsNullOrWhiteSpace(model.NewNidDocument.ContentType) ? "application/pdf" : model.NewNidDocument.ContentType;
                existingNid.FileSizeBytes = model.NewNidDocument.Length;
                existingNid.Status = VerificationStatus.Pending;
                existingNid.UploadedAt = DateTime.UtcNow;
            }
            else
            {
                var docId = Guid.NewGuid();
                property.Documents.Add(new PropertyDocument
                {
                    Id = docId,
                    PropertyId = property.Id,
                    DocumentType = "NID",
                    FileName = model.NewNidDocument.FileName,
                    FileData = nidBytes,
                    StorageReference = $"db://documents/{docId}",
                    FilePath = $"db://documents/{docId}",
                    ContentType = string.IsNullOrWhiteSpace(model.NewNidDocument.ContentType) ? "application/pdf" : model.NewNidDocument.ContentType,
                    FileSizeBytes = model.NewNidDocument.Length,
                    Status = VerificationStatus.Pending,
                    UploadedAt = DateTime.UtcNow
                });
            }
        }

        // Optional: Replace Deed Document directly in Supabase Database (bytea)
        if (model.NewPropertyDocument != null && model.NewPropertyDocument.Length > 0)
        {
            using var memoryStream = new MemoryStream();
            await model.NewPropertyDocument.CopyToAsync(memoryStream);
            var docBytes = memoryStream.ToArray();

            var existingDoc = property.Documents.FirstOrDefault(d => d.DocumentType != "NID");
            if (existingDoc != null)
            {
                existingDoc.FileName = model.NewPropertyDocument.FileName;
                existingDoc.FileData = docBytes;
                existingDoc.StorageReference = $"db://documents/{existingDoc.Id}";
                existingDoc.FilePath = $"db://documents/{existingDoc.Id}";
                existingDoc.ContentType = string.IsNullOrWhiteSpace(model.NewPropertyDocument.ContentType) ? "application/pdf" : model.NewPropertyDocument.ContentType;
                existingDoc.FileSizeBytes = model.NewPropertyDocument.Length;
                existingDoc.Status = VerificationStatus.Pending;
                existingDoc.UploadedAt = DateTime.UtcNow;
            }
            else
            {
                var docId = Guid.NewGuid();
                property.Documents.Add(new PropertyDocument
                {
                    Id = docId,
                    PropertyId = property.Id,
                    DocumentType = "Deed / Title",
                    FileName = model.NewPropertyDocument.FileName,
                    FileData = docBytes,
                    StorageReference = $"db://documents/{docId}",
                    FilePath = $"db://documents/{docId}",
                    ContentType = string.IsNullOrWhiteSpace(model.NewPropertyDocument.ContentType) ? "application/pdf" : model.NewPropertyDocument.ContentType,
                    FileSizeBytes = model.NewPropertyDocument.Length,
                    Status = VerificationStatus.Pending,
                    UploadedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        TempData["SubmissionSuccess"] = "Your listing has been updated and resubmitted for Admin review. Status is now PENDING.";
        return RedirectToAction("Index", "Profile");
    }

    // ==========================================
    // 5. BUY / INQUIRE / TRANSACTION ACTIONS
    // ==========================================
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("properties/{id:guid}/inquire")]
    public async Task<IActionResult> Inquire(Guid id, string message, string? contactPhone)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Challenge();

        var property = await _context.Properties.FindAsync(id);
        if (property == null || property.VerificationStatus != VerificationStatus.Approved)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["ErrorMessage"] = "Inquiry message cannot be blank.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var inquiry = new Inquiry
        {
            Id = Guid.NewGuid(),
            PropertyId = id,
            BuyerId = userId.Value,
            Message = message.Trim(),
            ContactPhone = contactPhone ?? "",
            ContactEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            CreatedAt = DateTime.UtcNow
        };

        _context.Inquiries.Add(inquiry);
        await _context.SaveChangesAsync();

        TempData["ToastMessage"] = "Your inquiry has been sent directly to the verified seller!";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("properties/{id:guid}/buy")]
    public async Task<IActionResult> Buy(Guid id, decimal? offerPrice, string? notes)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return Challenge();

        var property = await _context.Properties.FindAsync(id);
        if (property == null || property.VerificationStatus != VerificationStatus.Approved)
        {
            return NotFound();
        }

        if (property.SellerId == userId.Value)
        {
            TempData["ErrorMessage"] = "You cannot purchase your own property listing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var transaction = new PropertyTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = id,
            BuyerId = userId.Value,
            AgreedPrice = offerPrice ?? property.Price,
            Status = TransactionStatus.AgreementReached,
            Notes = notes ?? "Purchase offer placed through PropLink verified portal.",
            TransactionDate = DateTime.UtcNow
        };

        // Update listing transaction status to Negotiation or AgreementReached
        property.TransactionStatus = TransactionStatus.AgreementReached;

        _context.PropertyTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        TempData["ToastMessage"] = "Congratulations! Your purchase agreement has been registered in your Buying History.";
        return RedirectToAction("Index", "Profile");
    }

    private static string GetTimeAgo(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;
        if (span.TotalDays > 30) return $"{(int)(span.TotalDays / 30)} months ago";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays} days ago";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours} hours ago";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes} mins ago";
        return "Just now";
    }
}
