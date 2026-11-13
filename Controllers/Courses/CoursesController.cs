using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities;
using OpenAcademy.Entities.Courses;
using OpenAcademy.Responses;

namespace OpenAcademy.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.Instructor)]
    public async Task<ActionResult<ApiResponse<CourseDto>>> Create(
        [FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        if (User.IsInRole(RoleNames.Root) || !TryGetCurrentUserId(out var instructorId))
            return Forbid();

        var instructorExists = await dbContext.InstructorProfiles
            .AnyAsync(profile => profile.UserId == instructorId, ct);
        if (!instructorExists)
            return Forbid();

        var categoryIds = (request.CategoryIds ?? []).Distinct().ToArray();
        var categories = await dbContext.Categories
            .Where(category => categoryIds.Contains(category.Id))
            .ToListAsync(ct);
        if (categories.Count != categoryIds.Length)
            return BadRequest(ApiResponse<CourseDto>.Failed(
                "One or more categories were not found.", "category_not_found"));

        var course = new Course
        {
            InstructorId = instructorId,
            Title = request.Title,
            Description = request.Description,
            ThumbnailBlobPath = string.Empty,
            Status = CourseStatus.Draft
        };
        foreach (var category in categories)
            course.Categories.Add(category);

        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CourseDto>.Succeeded(CourseDto.From(course)));
    }

    [HttpPost("{courseId:guid}/enrollments")]
    [Authorize(Policy = PolicyNames.CanEnrollInCourse)]
    public async Task<ActionResult<ApiResponse<EnrollmentDto>>> Enroll(
        Guid courseId, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var studentId))
            return Forbid();

        var studentExists = await dbContext.StudentProfiles
            .AnyAsync(profile => profile.UserId == studentId, ct);
        if (!studentExists)
            return Forbid();

        var enrollment = await dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId, ct);
        var wasAlreadyActive = enrollment?.Status == EnrollmentStatus.Active;

        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                CourseId = courseId,
                StudentId = studentId,
                EnrollmentDate = DateTimeOffset.UtcNow,
                Status = EnrollmentStatus.Active
            };
            dbContext.Enrollments.Add(enrollment);
        }
        else if (!wasAlreadyActive)
        {
            enrollment.EnrollmentDate = DateTimeOffset.UtcNow;
            enrollment.Status = EnrollmentStatus.Active;
        }

        if (!wasAlreadyActive)
            await dbContext.SaveChangesAsync(ct);

        var result = ApiResponse<EnrollmentDto>.Succeeded(EnrollmentDto.From(enrollment));
        return wasAlreadyActive ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}
