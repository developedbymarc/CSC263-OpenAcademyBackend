using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Entities.Courses;

public class Lesson : ITimestamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    [Required] [MaxLength(128)] public string Title { get; set; } = null!;
    [Required] [MaxLength(1024)] public string Description { get; set; } = null!;

    // Markdown. LessonAssets are embedded in it as placeholders.
    // Unbounded `text` column: enforce a maximum length in request validation.
    [Required] public string Content { get; set; } = null!;

    [Range(0, int.MaxValue)] public int Order { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<LessonAsset> LessonAssets { get; } = new List<LessonAsset>();
}