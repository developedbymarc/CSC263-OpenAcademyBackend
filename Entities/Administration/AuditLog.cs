using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities.Users;

namespace OpenAcademy.Entities.Administration;

/// <summary>Append-only. Never update or delete rows.</summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary><c>null</c> = performed by the system (e.g. the moderation scan).</summary>
    public Guid? ActorId { get; set; }

    public ApplicationUser? Actor { get; set; }

    /// <summary>e.g. "Role.Assigned", "Admin.Created"</summary>
    [Required]
    [MaxLength(64)]
    public string Action { get; set; } = null!;

    // Plain strings, deliberately no FK: history must survive whatever happens to the target.
    [Required] [MaxLength(64)] public string TargetType { get; set; } = null!;
    [Required] [MaxLength(64)] public string TargetId { get; set; } = null!;

    /// <summary>Free-form context, JSON recommended.</summary>
    [MaxLength(4096)]
    public string? Details { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}