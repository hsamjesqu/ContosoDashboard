using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentActivity
{
    [Key]
    public int DocumentActivityId { get; set; }
    public int? DocumentId { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Detail { get; set; }
    public DateTime OccurredDate { get; set; } = DateTime.UtcNow;

    public virtual Document? Document { get; set; }
    public virtual User User { get; set; } = null!;
}

public static class DocumentActivityActions
{
    public const string Upload = "Upload";
    public const string Download = "Download";
    public const string Preview = "Preview";
    public const string Replace = "Replace";
    public const string Delete = "Delete";
    public const string Share = "Share";
}