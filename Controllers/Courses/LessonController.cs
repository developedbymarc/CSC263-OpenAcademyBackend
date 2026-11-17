using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Authorization;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities;
using OpenAcademy.Entities.Courses;
using OpenAcademy.Responses;

namespace OpenAcademy.Controllers;

[ApiController]
[Route("Courses/{courseId:guid}/Modules/{moduleId:guid}/Lessons")]
public sealed class LessonController(
    ApplicationDbContext dbContext,
    ICourseAccess courseAccess) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanReadCourseContent)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LessonDto>>>> List(
        Guid courseId, Guid moduleId, CancellationToken ct)
    {
        var moduleExists = await dbContext.Modules
            .AnyAsync(module => module.Id == moduleId && module.CourseId == courseId, ct);
        if (!moduleExists)
            return NotFound(ApiResponse<IReadOnlyList<LessonDto>>.Failed(
                "The module was not found.", "MODULE_NOT_FOUND"));

        var lessons = await dbContext.Lessons.AsNoTracking()
            .Where(lesson => lesson.ModuleId == moduleId && lesson.Module.CourseId == courseId)
            .OrderBy(lesson => lesson.Order)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(ct);

        var visibleLessons = new List<LessonDto>(lessons.Count);
        foreach (var lesson in lessons)
        {
            if (await courseAccess.CanAsync(User, CourseOperations.Read,
                    new CourseResource(CourseResourceKind.Lesson, lesson.Id), ct))
                visibleLessons.Add(LessonDto.From(lesson));
        }

        return Ok(ApiResponse<IReadOnlyList<LessonDto>>.Succeeded(visibleLessons));
    }

    [HttpGet("{lessonId:guid}")]
    [Authorize(Policy = PolicyNames.CanReadCourseContent)]
    public async Task<ActionResult<ApiResponse<LessonDto>>> Get(
        Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct)
    {
        var lesson = await dbContext.Lessons.AsNoTracking()
            .FirstOrDefaultAsync(lesson =>
                lesson.Id == lessonId
                && lesson.ModuleId == moduleId
                && lesson.Module.CourseId == courseId, ct);
        if (lesson is null)
            return NotFound(ApiResponse<LessonDto>.Failed("The lesson was not found.", "LESSON_NOT_FOUND"));

        if (!await courseAccess.CanAsync(User, CourseOperations.Read,
                new CourseResource(CourseResourceKind.Lesson, lesson.Id), ct))
            return Forbid();

        return Ok(ApiResponse<LessonDto>.Succeeded(LessonDto.From(lesson)));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.CanEditCourseContent)]
    public async Task<ActionResult<ApiResponse<LessonDto>>> Create(
        Guid courseId, Guid moduleId, [FromBody] CreateLessonRequest request, CancellationToken ct)
    {
        var moduleExists = await dbContext.Modules
            .AnyAsync(module => module.Id == moduleId && module.CourseId == courseId, ct);
        if (!moduleExists)
            return NotFound(ApiResponse<LessonDto>.Failed("The module was not found.", "module_not_found"));

        var lesson = new Lesson
        {
            ModuleId = moduleId,
            Title = request.Title,
            Description = request.Description,
            Content = request.Content,
            Order = request.Order,
            Status = LessonStatus.Draft
        };
        dbContext.Lessons.Add(lesson);
        await dbContext.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<LessonDto>.Succeeded(LessonDto.From(lesson)));
    }
}
