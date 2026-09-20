using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }
    public int DocumentId { get; set; }
    public int? UserId { get; set; }
    public int? ProjectId { get; set; }
    public int SharedByUserId { get; set; }
    public DateTime SharedDate { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedDate { get; set; }

    public virtual Document Document { get; set; } = null!;
    public virtual User? User { get; set; }
    public virtual Project? Project { get; set; }
    public virtual User SharedByUser { get; set; } = null!;
}