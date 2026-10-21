using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities.Users;
using OpenAcademy.Entities.Users.RoleProfiles;

namespace OpenAcademy.Entities.Moderation;

public class Report : ITimestamped
{
    public long Id { get; set; }

    /// <summary>
    /// The user who filed the report. <c>null</c> = raised by the automated scan.
    /// </summary>
    public Guid? IssuerId { get; set; }

    public User? Issuer { get; set; }

    /// <summary>
    /// Best case: An actual entity type from <code>OpenAcademy.Entities</code>
    /// Worst case: <code>null</code> 
    /// </summary>
    [MaxLength(64)]
    public string? ContentType { get; set; }

    [MaxLength(64)] public string? ContentId { get; set; }
    [MaxLength(512)] public string? Description { get; set; }
    public ReportCreationReason Reason { get; set; }


    // metadata
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public bool IsClosed => ClosedAt.HasValue;

    // Both are null until someone picks the report up / escalates it.
    public Guid? AssignedToId { get; set; }
    public ModeratorProfile? AssignedTo { get; set; }
    public Guid? ElevatedToId { get; set; }
    public AdminProfile? ElevatedTo { get; set; }

    /// <summary>
    /// could be either admin or mod. Admin if the report got elevated, mod if they handled it alone.
    /// Should probably be enforced in some way that it is either ElevatedTo or AssignedTo that handle the closure.
    /// (The DB only guarantees that ClosedAt, ClosedById and ClosureReason are set together; "who may close" stays in the service layer.)
    /// </summary>
    public Guid? ClosedById { get; set; }

    public User? ClosedBy { get; set; }
    public ReportClosureReason? ClosureReason { get; set; }

    /// <summary>The documented decision ("let Admins document their decisions").</summary>
    [MaxLength(2048)]
    public string? ClosureNote { get; set; }
}