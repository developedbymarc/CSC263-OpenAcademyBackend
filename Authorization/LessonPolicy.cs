using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Constants;
using OpenAcademy.Data;

namespace OpenAcademy.Authorization;

public enum LessonAccessLevel { Read, Write }

public sealed class LessonAccessRequirement(LessonAccessLevel level) : IAuthorizationRequirement
{
    public LessonAccessLevel Level { get; } = level;
}

/// <summary>
/// Named-policy version. The lesson is identified by the {lessonId} route value, so any action
/// with that route parameter can simply say [Authorize(Policy = PolicyNames.CanReadLesson)].
///   Read:  course instructor, enrolled student, Admin, Moderator
///   Write: course instructor only
/// Fails closed: missing/invalid route value, unknown lesson, or non-matching user => no Succeed => 403.
/// </summary>
public sealed class LessonAccessPolicyHandler(ApplicationDbContext db)
    : AuthorizationHandler<LessonAccessRequirement>
{
    private const string RouteKey = "lessonId";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, LessonAccessRequirement requirement)
    {
        var http = context.Resource switch
        {
            HttpContext h => h,                       // endpoint routing (the normal case)
            AuthorizationFilterContext f => f.HttpContext,
            _ => null
        };
        if (http is null) return;

        if (!http.Request.RouteValues.TryGetValue(RouteKey, out var routeValue)
            || !Guid.TryParse(routeValue?.ToString(), out var lessonId)) return;

        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return;

        var ct = http.RequestAborted;

        var course = await db.Lessons.AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => new { l.Module.CourseId, l.Module.Course.InstructorId })
            .SingleOrDefaultAsync(ct);
        if (course is null) return;

        if (course.InstructorId == userId)
        {
            context.Succeed(requirement);
            return;
        }

        if (requirement.Level != LessonAccessLevel.Read) return;

        if (context.User.IsInRole(RoleNames.Admin) || context.User.IsInRole(RoleNames.Moderator))
        {
            context.Succeed(requirement);
            return;
        }

        if (await db.Enrollments.AsNoTracking()
                .AnyAsync(e => e.CourseId == course.CourseId && e.StudentId == userId, ct))
            context.Succeed(requirement);
    }
}

public static class LessonPolicyExtensions
{
    public static IServiceCollection AddLessonPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.CanReadLesson, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new LessonAccessRequirement(LessonAccessLevel.Read)))
            .AddPolicy(PolicyNames.CanEditLesson, p => p
                .RequireRole(RoleNames.Instructor)
                .AddRequirements(new LessonAccessRequirement(LessonAccessLevel.Write)));

        services.AddScoped<IAuthorizationHandler, LessonAccessPolicyHandler>();
        return services;
    }
}