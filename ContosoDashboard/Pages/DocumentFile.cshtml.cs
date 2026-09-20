using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using ContosoDashboard.Services;
using ContosoDashboard.Models;

namespace ContosoDashboard.Pages;

[Authorize]
public class DocumentFileModel : PageModel
{
    private readonly IDocumentService _documents;
    private readonly IFileStorageService _storage;
    private readonly IDocumentAuditService _audit;

    public DocumentFileModel(IDocumentService documents, IFileStorageService storage, IDocumentAuditService audit)
    {
        _documents = documents; _storage = storage; _audit = audit;
    }

    public async Task<IActionResult> OnGetAsync(int documentId, string mode = "download")
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var document = await _documents.GetAccessibleAsync(documentId, userId);
        if (document == null) return NotFound();
        var stream = await _storage.OpenReadAsync(document.FilePath);
        if (stream == null) return NotFound();

        var preview = string.Equals(mode, "preview", StringComparison.OrdinalIgnoreCase) &&
            (document.FileType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || document.FileType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
        if (preview)
        {
            Response.Headers.ContentDisposition = $"inline; filename=\"{Path.GetFileName(document.OriginalFileName)}\"";
            await _audit.RecordAsync(document.DocumentId, userId, DocumentActivityActions.Preview);
            return new FileStreamResult(stream, document.FileType);
        }

        await _audit.RecordAsync(document.DocumentId, userId, DocumentActivityActions.Download);
        return new FileStreamResult(stream, document.FileType)
        {
            FileDownloadName = Path.GetFileName(document.OriginalFileName),
            EnableRangeProcessing = true
        };
    }
}