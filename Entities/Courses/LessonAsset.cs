using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Entities.Courses;

public class LessonAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    /// <summary>Object key in blob storage (never the file itself).</summary>
    [Required]
    [MaxLength(1024)]
    public string BlobPath { get; set; } = null!;

    public AssetType Type { get; set; }
    public AssetStatus Status { get; set; }

    /// <summary>
    /// e.g. image/webp
    /// </summary>
    [Required]
    [MaxLength(127)]
    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }
    public int? DurationSeconds { get; set; }

    [Range(0, int.MaxValue)] public int Order { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}