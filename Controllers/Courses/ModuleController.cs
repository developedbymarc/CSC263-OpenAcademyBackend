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
[Route("api/courses/{courseId:guid}/modules")]
public sealed class ModuleController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanEditCourseContent)]
    public async Task<ActionResult<ApiResponse<ModuleDto>>> Create(
        Guid courseId, [FromBody] CreateModuleRequest request, CancellationToken ct)
    {
        var courseExists = await dbContext.Courses.AnyAsync(course => course.Id == courseId, ct);
        if (!courseExists)
            return NotFound(ApiResponse<ModuleDto>.Failed("The course was not found.", "course_not_found"));

        var module = new Module
        {
            CourseId = courseId,
            Title = request.Title,
            Description = request.Description,
            Order = request.Order,
            Status = ModuleStatus.Draft
        };
        dbContext.Modules.Add(module);
        await dbContext.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ModuleDto>.Succeeded(ModuleDto.From(module)));
    }
}