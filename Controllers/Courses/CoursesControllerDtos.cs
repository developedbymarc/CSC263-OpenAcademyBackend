using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities;

namespace OpenAcademy.Controllers;

public sealed record CreateCourseRequest(
    [Required, MaxLength(128)] string Title,
    [Required, MaxLength(1024)] string Description,
    IReadOnlyCollection<int>? CategoryIds = null);

public sealed record CourseDto(
    Guid Id,
    Guid InstructorId,
    string Title,
    string Description,
    string? ThumbnailBlobPath,
    CourseStatus Status)
{
    public static CourseDto From(Entities.Courses.Course course) => new(
        course.Id,
        course.InstructorId,
        course.Title,
        course.Description,
        course.ThumbnailBlobPath,
        course.Status);
}

public sealed record EnrollmentDto(
    Guid CourseId,
    Guid StudentId,
    DateTimeOffset EnrollmentDate,
    EnrollmentStatus Status)
{
    public static EnrollmentDto From(Entities.Courses.Enrollment enrollment) => new(
        enrollment.CourseId,
        enrollment.StudentId,
        enrollment.EnrollmentDate,
        enrollment.Status);
}

