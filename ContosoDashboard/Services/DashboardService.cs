using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(int userId);
    Task<List<Announcement>> GetActiveAnnouncementsAsync();
    Task<int> GetDocumentCountAsync(int userId);
    Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5);
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IDocumentService _documents;

    public DashboardService(ApplicationDbContext context, IDocumentService documents)
    {
        _context = context;
        _documents = documents;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var summary = new DashboardSummary
        {
            TotalActiveTasks = await _context.Tasks
                .CountAsync(t => t.AssignedUserId == userId && t.Status != Models.TaskStatus.Completed),

            TasksDueToday = await _context.Tasks
                .CountAsync(t => t.AssignedUserId == userId 
                    && t.DueDate.HasValue 
                    && t.DueDate.Value.Date == now.Date
                    && t.Status != Models.TaskStatus.Completed),

            ActiveProjects = await _context.Projects
                .Where(p => p.ProjectManagerId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId))
                .Where(p => p.Status == ProjectStatus.Active)
                .CountAsync(),

            UnreadNotifications = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead)
        };

        summary.DocumentCount = await GetDocumentCountAsync(userId);
        summary.RecentDocuments = await GetRecentDocumentsAsync(userId);

        return summary;
    }

    public async Task<int> GetDocumentCountAsync(int userId) =>
        (await _documents.SearchAsync(new DocumentQuery { PageSize = 1 }, userId)).TotalCount;

    public async Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5) =>
        (await _documents.SearchAsync(new DocumentQuery { PageSize = Math.Clamp(count, 1, 50), SortBy = "date" }, userId)).Items.ToList();

    public async Task<List<Announcement>> GetActiveAnnouncementsAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a => a.IsActive 
                && a.PublishDate <= now 
                && (!a.ExpiryDate.HasValue || a.ExpiryDate.Value > now))
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .ToListAsync();
    }
}

public class DashboardSummary
{
    public int TotalActiveTasks { get; set; }
    public int TasksDueToday { get; set; }
    public int ActiveProjects { get; set; }
    public int UnreadNotifications { get; set; }
    public int DocumentCount { get; set; }
    public List<Document> RecentDocuments { get; set; } = [];
}
