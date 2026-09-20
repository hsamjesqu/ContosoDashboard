using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentAuditService
{
    Task<DocumentActivity> RecordAsync(int? documentId, int userId, string action, string? detail = null);
    Task<List<DocumentActivity>> GetRetainedActivityAsync(int requestingUserId, int? documentId = null, DateTime? since = null);
    Task<DocumentReport> GetReportAsync(int requestingUserId, DateTime? since = null, DateTime? until = null);
}

public sealed class DocumentAuditService : IDocumentAuditService
{
    private readonly ApplicationDbContext _context;

    public DocumentAuditService(ApplicationDbContext context) => _context = context;

    public async Task<DocumentActivity> RecordAsync(int? documentId, int userId, string action, string? detail = null)
    {
        var activity = new DocumentActivity
        {
            DocumentId = documentId,
            UserId = userId,
            Action = action,
            Detail = detail,
            OccurredDate = DateTime.UtcNow
        };
        _context.DocumentActivities.Add(activity);
        await _context.SaveChangesAsync();
        return activity;
    }

    public async Task<List<DocumentActivity>> GetRetainedActivityAsync(int requestingUserId, int? documentId = null, DateTime? since = null)
    {
        if (!await IsAdministratorAsync(requestingUserId)) return [];
        var query = _context.DocumentActivities.AsNoTracking().AsQueryable();
        if (documentId.HasValue) query = query.Where(a => a.DocumentId == documentId);
        if (since.HasValue) query = query.Where(a => a.OccurredDate >= since.Value);
        return await query.OrderByDescending(a => a.OccurredDate).ToListAsync();
    }

    public async Task<DocumentReport> GetReportAsync(int requestingUserId, DateTime? since = null, DateTime? until = null)
    {
        if (!await IsAdministratorAsync(requestingUserId)) throw new UnauthorizedAccessException();
        var activities = _context.DocumentActivities.AsNoTracking();
        if (since.HasValue) activities = activities.Where(a => a.OccurredDate >= since.Value);
        if (until.HasValue) activities = activities.Where(a => a.OccurredDate <= until.Value);

        var rows = await activities.ToListAsync();
        var documentIds = rows.Where(a => a.DocumentId.HasValue).Select(a => a.DocumentId!.Value).Distinct().ToList();
        var documents = await _context.Documents.AsNoTracking().Where(d => documentIds.Contains(d.DocumentId)).ToListAsync();
        return new DocumentReport
        {
            DocumentTypes = documents.GroupBy(d => d.FileType).ToDictionary(g => g.Key, g => g.Count()),
            ActiveUploaders = documents.Where(d => !d.IsDeleted).GroupBy(d => d.UploadedByUserId).ToDictionary(g => g.Key, g => g.Count()),
            AccessPatterns = rows.GroupBy(a => a.Action).ToDictionary(g => g.Key, g => g.Count()),
            Activity = rows.OrderByDescending(a => a.OccurredDate).ToList()
        };
    }

    private Task<bool> IsAdministratorAsync(int userId) => _context.Users.AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator);
}

public sealed class DocumentReport
{
    public Dictionary<string, int> DocumentTypes { get; init; } = [];
    public Dictionary<int, int> ActiveUploaders { get; init; } = [];
    public Dictionary<string, int> AccessPatterns { get; init; } = [];
    public List<DocumentActivity> Activity { get; init; } = [];
}