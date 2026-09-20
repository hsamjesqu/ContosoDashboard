using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public sealed class DocumentUploadRequest
{
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = DocumentCategories.Other;
    public string? Description { get; init; }
    public IEnumerable<string> Tags { get; init; } = [];
    public int? ProjectId { get; init; }
    public int? TaskId { get; init; }
}

public sealed class DocumentQuery
{
    public string? Search { get; init; }
    public string? Category { get; init; }
    public int? ProjectId { get; init; }
    public int? UploaderId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string SortBy { get; init; } = "date";
    public bool Descending { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record DocumentPage(IReadOnlyList<Document> Items, int TotalCount, int Page, int PageSize);

public interface IDocumentService
{
    Task<Document> UploadAsync(DocumentUploadRequest request, int userId, CancellationToken cancellationToken = default);
    Task<DocumentPage> SearchAsync(DocumentQuery query, int userId);
    Task<Document?> GetAccessibleAsync(int documentId, int userId, bool includeDeleted = false);
    Task<bool> CanAccessAsync(int documentId, int userId);
    Task<bool> UpdateMetadataAsync(int documentId, int userId, string title, string category, string? description, IEnumerable<string> tags);
    Task<bool> ReplaceAsync(int documentId, int userId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int documentId, int userId, CancellationToken cancellationToken = default);
    Task<DocumentShare> ShareAsync(int documentId, int userId, int? recipientUserId = null, int? projectId = null);
    Task<bool> RevokeShareAsync(int documentShareId, int userId);
    Task<List<Document>> GetSharedWithMeAsync(int userId);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int userId);
    Task<List<Document>> GetTaskDocumentsAsync(int taskId, int userId);
}

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IFileScanService _scanner;
    private readonly IDocumentAuditService _audit;
    private readonly DocumentStorageOptions _options;
    private readonly INotificationService _notifications;

    public DocumentService(ApplicationDbContext context, IFileStorageService storage, IFileScanService scanner,
        IDocumentAuditService audit, IOptions<DocumentStorageOptions> options, INotificationService notifications)
    {
        _context = context;
        _storage = storage;
        _scanner = scanner;
        _audit = audit;
        _options = options.Value;
        _notifications = notifications;
    }

    public async Task<Document> UploadAsync(DocumentUploadRequest request, int userId, CancellationToken cancellationToken = default)
    {
        await ValidateUploadAsync(request, userId);
        var scan = await ScanAsync(request.Content, request.FileName, cancellationToken);
        if (scan.Status != FileScanStatus.Safe) throw new InvalidDataException(scan.Reason ?? "The file could not be approved.");

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        StoredFile? stored = null;
        Document? document = null;
        try
        {
            stored = await _storage.SaveAsync(request.Content, userId.ToString(), request.ProjectId, extension, cancellationToken);
            document = BuildDocument(request, userId, stored);
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
            await AddTagsAsync(document, request.Tags, cancellationToken);
            await _audit.RecordAsync(document.DocumentId, userId, DocumentActivityActions.Upload, request.OriginalFileName());
            await NotifyProjectMembersAsync(document, userId);
            return document;
        }
        catch
        {
            if (stored != null) await _storage.DeleteAsync(stored.RelativePath, cancellationToken);
            if (document?.DocumentId > 0) _context.Documents.Remove(document);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DocumentPage> SearchAsync(DocumentQuery query, int userId)
    {
        var baseQuery = AccessibleQuery(userId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(d => d.Title.ToLower().Contains(search) ||
                (d.Description != null && d.Description.ToLower().Contains(search)) ||
                d.Tags.Any(t => t.Value.Contains(search)) || d.OriginalFileName.ToLower().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.Category)) baseQuery = baseQuery.Where(d => d.Category == query.Category);
        if (query.ProjectId.HasValue) baseQuery = baseQuery.Where(d => d.ProjectId == query.ProjectId);
        if (query.UploaderId.HasValue) baseQuery = baseQuery.Where(d => d.UploadedByUserId == query.UploaderId);
        if (query.From.HasValue) baseQuery = baseQuery.Where(d => d.UploadedDate >= query.From);
        if (query.To.HasValue) baseQuery = baseQuery.Where(d => d.UploadedDate <= query.To);

        baseQuery = query.SortBy.ToLowerInvariant() switch
        {
            "title" => query.Descending ? baseQuery.OrderByDescending(d => d.Title) : baseQuery.OrderBy(d => d.Title),
            "category" => query.Descending ? baseQuery.OrderByDescending(d => d.Category) : baseQuery.OrderBy(d => d.Category),
            "size" => query.Descending ? baseQuery.OrderByDescending(d => d.FileSize) : baseQuery.OrderBy(d => d.FileSize),
            _ => query.Descending ? baseQuery.OrderByDescending(d => d.UploadedDate) : baseQuery.OrderBy(d => d.UploadedDate)
        };
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var total = await baseQuery.CountAsync();
        var items = await baseQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new DocumentPage(items, total, page, pageSize);
    }

    public async Task<Document?> GetAccessibleAsync(int documentId, int userId, bool includeDeleted = false)
    {
        var document = await _context.Documents
            .Include(d => d.Project).ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Shares)
            .Include(d => d.Tags)
            .Include(d => d.UploadedByUser)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && (includeDeleted || !d.IsDeleted));
        return document != null && await HasAccessAsync(document, userId) ? document : null;
    }

    public async Task<bool> CanAccessAsync(int documentId, int userId) => await GetAccessibleAsync(documentId, userId) != null;

    public async Task<bool> UpdateMetadataAsync(int documentId, int userId, string title, string category, string? description, IEnumerable<string> tags)
    {
        var document = await GetAccessibleAsync(documentId, userId);
        if (document == null || !await CanManageAsync(document, userId)) return false;
        ValidateMetadata(title, category);
        document.Title = title.Trim(); document.Category = category; document.Description = description;
        _context.DocumentTags.RemoveRange(document.Tags);
        await AddTagsAsync(document, tags, CancellationToken.None);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReplaceAsync(int documentId, int userId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var document = await GetAccessibleAsync(documentId, userId);
        if (document == null || !await CanManageAsync(document, userId)) return false;
        var request = new DocumentUploadRequest { Content = content, FileName = fileName, ContentType = contentType, Title = document.Title, Category = document.Category, ProjectId = document.ProjectId, TaskId = document.TaskId };
        await ValidateUploadAsync(request, userId);
        var scan = await ScanAsync(content, fileName, cancellationToken);
        if (scan.Status != FileScanStatus.Safe) throw new InvalidDataException(scan.Reason ?? "The replacement could not be approved.");
        var oldPath = document.FilePath;
        var stored = await _storage.SaveAsync(content, userId.ToString(), document.ProjectId, Path.GetExtension(fileName), cancellationToken);
        try
        {
            document.FilePath = stored.RelativePath; document.OriginalFileName = fileName; document.FileType = contentType; document.FileSize = stored.Length; document.UploadedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(stored.RelativePath, cancellationToken);
            throw;
        }
        await _storage.DeleteAsync(oldPath, cancellationToken);
        await _audit.RecordAsync(document.DocumentId, userId, DocumentActivityActions.Replace, fileName);
        return true;
    }

    public async Task<bool> DeleteAsync(int documentId, int userId, CancellationToken cancellationToken = default)
    {
        var document = await GetAccessibleAsync(documentId, userId);
        if (document == null || !await CanManageAsync(document, userId)) return false;
        document.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
        await _storage.DeleteAsync(document.FilePath, cancellationToken);
        await _audit.RecordAsync(document.DocumentId, userId, DocumentActivityActions.Delete);
        return true;
    }

    public async Task<DocumentShare> ShareAsync(int documentId, int userId, int? recipientUserId = null, int? projectId = null)
    {
        if ((recipientUserId.HasValue ? 1 : 0) + (projectId.HasValue ? 1 : 0) != 1) throw new ArgumentException("Exactly one share target is required.");
        var document = await GetAccessibleAsync(documentId, userId);
        if (document == null || document.UploadedByUserId != userId) throw new UnauthorizedAccessException();
        if (projectId.HasValue && document.ProjectId != projectId) throw new InvalidOperationException("The share project must match the document project.");
        if (recipientUserId.HasValue && !await _context.Users.AnyAsync(u => u.UserId == recipientUserId)) throw new InvalidOperationException("The recipient does not exist.");
        var share = await _context.DocumentShares.FirstOrDefaultAsync(s => s.DocumentId == documentId && s.UserId == recipientUserId && s.ProjectId == projectId);
        if (share == null) { share = new DocumentShare { DocumentId = documentId, UserId = recipientUserId, ProjectId = projectId, SharedByUserId = userId }; _context.DocumentShares.Add(share); }
        else share.RevokedDate = null;
        await _context.SaveChangesAsync();
        await _audit.RecordAsync(documentId, userId, DocumentActivityActions.Share, recipientUserId?.ToString() ?? $"project:{projectId}");
        if (recipientUserId.HasValue) await NotifyAsync(recipientUserId.Value, "Document shared", $"A document was shared with you: {document.Title}");
        return share;
    }

    public async Task<bool> RevokeShareAsync(int documentShareId, int userId)
    {
        var share = await _context.DocumentShares.Include(s => s.Document).FirstOrDefaultAsync(s => s.DocumentShareId == documentShareId);
        if (share == null || share.Document.UploadedByUserId != userId) return false;
        share.RevokedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<List<Document>> GetSharedWithMeAsync(int userId) => AccessibleQuery(userId).Where(d => d.UploadedByUserId != userId && d.Shares.Any(s => s.UserId == userId && s.RevokedDate == null)).OrderByDescending(d => d.UploadedDate).ToListAsync();
    public async Task<List<Document>> GetProjectDocumentsAsync(int projectId, int userId) => (await SearchAsync(new DocumentQuery { ProjectId = projectId }, userId)).Items.ToList();
    public async Task<List<Document>> GetTaskDocumentsAsync(int taskId, int userId) => (await SearchAsync(new DocumentQuery { }, userId)).Items.Where(d => d.TaskId == taskId).ToList();

    private IQueryable<Document> AccessibleQuery(int userId) => _context.Documents.AsNoTracking().Include(d => d.Project).Include(d => d.UploadedByUser).Include(d => d.Tags).Where(d => !d.IsDeleted && (d.UploadedByUserId == userId || (d.Project != null && (d.Project.ProjectManagerId == userId || d.Project.ProjectMembers.Any(pm => pm.UserId == userId))) || d.Shares.Any(s => s.UserId == userId && s.RevokedDate == null) || _context.Users.Any(u => u.UserId == userId && u.Role == UserRole.Administrator)));

    private async Task<bool> HasAccessAsync(Document document, int userId) => document.UploadedByUserId == userId || document.Project?.ProjectManagerId == userId || document.Project?.ProjectMembers.Any(pm => pm.UserId == userId) == true || document.Shares.Any(s => s.UserId == userId && s.RevokedDate == null) || await _context.Users.AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator);
    private async Task<bool> CanManageAsync(Document document, int userId) => document.UploadedByUserId == userId || document.Project?.ProjectManagerId == userId || await _context.Users.AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator);
    private async Task ValidateUploadAsync(DocumentUploadRequest request, int userId)
    {
        ValidateMetadata(request.Title, request.Category);
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || !_options.AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("The file type is not allowed.");
        if (request.Content == null || request.Content.Length <= 0 || request.Content.Length > _options.MaxFileSizeBytes) throw new InvalidDataException("The file size is invalid.");
        if (request.ContentType.Length > 255) throw new InvalidDataException("The content type is too long.");
        if (!await CanUseAssociationAsync(request.ProjectId, request.TaskId, userId)) throw new UnauthorizedAccessException();
    }
    private static void ValidateMetadata(string title, string category) { if (string.IsNullOrWhiteSpace(title) || title.Length > 255) throw new ArgumentException("A title is required."); if (!DocumentCategories.All.Contains(category)) throw new ArgumentException("The category is invalid."); }
    private async Task<bool> CanUseAssociationAsync(int? projectId, int? taskId, int userId)
    {
        if (taskId.HasValue) { var task = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.TaskId == taskId); if (task == null || task.ProjectId != projectId) return false; }
        if (!projectId.HasValue) return !taskId.HasValue;
        return await _context.Projects.AnyAsync(p => p.ProjectId == projectId && (p.ProjectManagerId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId) || _context.Users.Any(u => u.UserId == userId && u.Role == UserRole.Administrator)));
    }
    private async Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken) { if (content.CanSeek) content.Position = 0; var result = await _scanner.ScanAsync(content, fileName, cancellationToken); if (content.CanSeek) content.Position = 0; return result; }
    private static Document BuildDocument(DocumentUploadRequest request, int userId, StoredFile stored) => new() { Title = request.Title.Trim(), Description = request.Description, Category = request.Category, OriginalFileName = Path.GetFileName(request.FileName), FilePath = stored.RelativePath, FileType = request.ContentType, FileSize = stored.Length, UploadedByUserId = userId, ProjectId = request.ProjectId, TaskId = request.TaskId };
    private async Task AddTagsAsync(Document document, IEnumerable<string> tags, CancellationToken cancellationToken) { foreach (var tag in tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().Take(20).Where(t => t.Length <= 100)) document.Tags.Add(new DocumentTag { Document = document, Value = tag }); await _context.SaveChangesAsync(cancellationToken); }
    private async Task NotifyProjectMembersAsync(Document document, int actorId) { if (!document.ProjectId.HasValue) return; var ids = await _context.ProjectMembers.Where(pm => pm.ProjectId == document.ProjectId && pm.UserId != actorId).Select(pm => pm.UserId).ToListAsync(); foreach (var id in ids) await NotifyAsync(id, "Project document added", $"A document was added to your project: {document.Title}"); }
    private async Task NotifyAsync(int userId, string title, string message) { if (await _context.Users.AnyAsync(u => u.UserId == userId && u.InAppNotificationsEnabled)) await _notifications.CreateNotificationAsync(new Notification { UserId = userId, Title = title, Message = message, Type = NotificationType.ProjectUpdate, Priority = NotificationPriority.Informational }); }
}

file static class UploadRequestExtensions
{
    public static string OriginalFileName(this DocumentUploadRequest request) => Path.GetFileName(request.FileName);
}