using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class Document
{
    public const long MaximumFileSize = 25 * 1024 * 1024;

    [Key]
    public int DocumentId { get; set; }

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = DocumentCategories.Other;

    [Required, MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    public long FileSize { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }
    public bool IsDeleted { get; set; }

    public virtual User UploadedByUser { get; set; } = null!;
    public virtual Project? Project { get; set; }
    public virtual TaskItem? Task { get; set; }
    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    public virtual ICollection<DocumentActivity> Activities { get; set; } = new List<DocumentActivity>();
    public virtual ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
}