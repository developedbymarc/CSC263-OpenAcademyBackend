using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities;

namespace OpenAcademy.Controllers;

public sealed record CreateLessonRequest(
    [Required, MaxLength(128)] string Title,
    [Required, MaxLength(1024)] string Description,
    [Required, MaxLength(1000000)] string Content,
    [Range(0, int.MaxValue)] int Order);

public sealed record LessonDto(
    Guid Id,
    Guid ModuleId,
    string Title,
    string Description,
    string Content,
    int Order,
    LessonStatus Status)
{
    public static LessonDto From(Entities.Courses.Lesson lesson) => new(
        lesson.Id,
        lesson.ModuleId,
        lesson.Title,
        lesson.Description,
        lesson.Content,
        lesson.Order,
        lesson.Status);
}

