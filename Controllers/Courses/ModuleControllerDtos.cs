using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities;

namespace OpenAcademy.Controllers;

public sealed record CreateModuleRequest(
    [Required, MaxLength(128)] string Title,
    [Required, MaxLength(1024)] string Description,
    [Range(0, int.MaxValue)] int Order);

public sealed record ModuleDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string Description,
    int Order,
    ModuleStatus Status)
{
    public static ModuleDto From(Entities.Courses.Module module) => new(
        module.Id,
        module.CourseId,
        module.Title,
        module.Description,
        module.Order,
        module.Status);
}

