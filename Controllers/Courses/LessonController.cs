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
[Route("api/modules/{moduleId:guid}/lessons")]
public sealed class LessonController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanEditCourseContent)]
    public async Task<ActionResult<ApiResponse<LessonDto>>> Create(
        Guid moduleId, [FromBody] CreateLessonRequest request, CancellationToken ct)
    {
        var moduleExists = await dbContext.Modules.AnyAsync(module => module.Id == moduleId, ct);
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