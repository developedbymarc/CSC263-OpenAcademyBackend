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
[Route("Courses/{courseId:guid}/Modules")]
public sealed class ModuleController(
    ApplicationDbContext dbContext,
    ICourseAccess courseAccess) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanReadCourseContent)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ModuleDto>>>> List(
        Guid courseId, CancellationToken ct)
    {
        var modules = await dbContext.Modules.AsNoTracking()
            .Where(module => module.CourseId == courseId)
            .OrderBy(module => module.Order)
            .ThenBy(module => module.Id)
            .ToListAsync(ct);

        var visibleModules = new List<ModuleDto>(modules.Count);
        foreach (var module in modules)
        {
            if (await courseAccess.CanAsync(User, CourseOperations.Read,
                    new CourseResource(CourseResourceKind.Module, module.Id), ct))
                visibleModules.Add(ModuleDto.From(module));
        }

        return Ok(ApiResponse<IReadOnlyList<ModuleDto>>.Succeeded(visibleModules));
    }

    [HttpGet("{moduleId:guid}")]
    [Authorize(Policy = PolicyNames.CanReadCourseContent)]
    public async Task<ActionResult<ApiResponse<ModuleDto>>> Get(
        Guid courseId, Guid moduleId, CancellationToken ct)
    {
        var module = await dbContext.Modules.AsNoTracking()
            .FirstOrDefaultAsync(module => module.Id == moduleId && module.CourseId == courseId, ct);
        if (module is null)
            return NotFound(ApiResponse<ModuleDto>.Failed("The module was not found.", "MODULE_NOT_FOUND"));

        if (!await courseAccess.CanAsync(User, CourseOperations.Read,
                new CourseResource(CourseResourceKind.Module, module.Id), ct))
            return Forbid();

        return Ok(ApiResponse<ModuleDto>.Succeeded(ModuleDto.From(module)));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.CanEditCourseContent)]
    public async Task<ActionResult<ApiResponse<ModuleDto>>> Create(
        Guid courseId, [FromBody] CreateModuleRequest request, CancellationToken ct)
    {
        var courseExists = await dbContext.Courses.AnyAsync(course => course.Id == courseId, ct);
        if (!courseExists)
            return NotFound(ApiResponse<ModuleDto>.Failed("The course was not found.", "COURSE_NOT_FOUND"));

        var module = new Module
        {
            CourseId = courseId,
            Title = request.Title,
            Description = request.Description,
            Order = request.Order,
            Status = ModuleStatus.Published
        };
        dbContext.Modules.Add(module);
        await dbContext.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ModuleDto>.Succeeded(ModuleDto.From(module)));
    }
}
