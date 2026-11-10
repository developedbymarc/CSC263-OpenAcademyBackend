using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities;

namespace OpenAcademy.Authorization;

// ---------------------------------------------------------------------------------------------
// Course content access
//
// Access is decided once, at the COURSE, and inherited by modules, lessons and lesson assets.
//
//   Operation   Instructor (owner)   Enrolled student      Admin   Moderator
//   Read        yes                  yes, if all visible   yes     yes
//   Edit        yes
//   Delete      yes
//   Publish     yes (unless held)
//   Unpublish   yes                                        yes
//
// Root is always refused. Everything fails closed: unknown resource, malformed id, no user => no.
//
// Assumed model (rename to match yours):
//   Course        Id, InstructorId, IsPublished, ModerationHold
//   Module        Id, CourseId, Course, IsPublished            (DbSet: Modules)
//   Lesson        Id, Module, IsPublished                      (DbSet: Lessons)
//   LessonAsset   Id, Lesson                                   (DbSet: LessonAssets)
//   Enrollment    CourseId, StudentId                          (DbSet: Enrollments)
// ---------------------------------------------------------------------------------------------

public enum CourseResourceKind { Course, Module, Lesson, Asset }

/// <summary>A reference to anything that lives under a course.</summary>
public readonly record struct CourseResource(CourseResourceKind Kind, Guid Id);

public static class CourseOperations
{
    public static readonly OperationAuthorizationRequirement Read      = new() { Name = nameof(Read) };
    public static readonly OperationAuthorizationRequirement Edit      = new() { Name = nameof(Edit) };
    public static readonly OperationAuthorizationRequirement Delete    = new() { Name = nameof(Delete) };
    public static readonly OperationAuthorizationRequirement Publish   = new() { Name = nameof(Publish) };
    public static readonly OperationAuthorizationRequirement Unpublish = new() { Name = nameof(Unpublish) };

    internal static bool IsCourseOperation(OperationAuthorizationRequirement r) =>
        r.Name is nameof(Read) or nameof(Edit) or nameof(Delete) or nameof(Publish) or nameof(Unpublish);
}

// ------------------------------------ the decision ------------------------------------------

public interface ICourseAccess
{
    Task<bool> CanAsync(
        ClaimsPrincipal user,
        OperationAuthorizationRequirement operation,
        CourseResource resource,
        CancellationToken ct = default);
}

internal sealed class CourseAccess(ApplicationDbContext db) : ICourseAccess
{
    private sealed record Facts(
        Guid InstructorId,
        bool ModerationHold,
        bool Visible,     // course, module and lesson are all published, as far as the resource goes
        bool Enrolled);

    public async Task<bool> CanAsync(
        ClaimsPrincipal user,
        OperationAuthorizationRequirement operation,
        CourseResource resource,
        CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole(RoleNames.Root)) return false;                 // Root: no operational actions

        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return false;

        var facts = await LoadAsync(resource, userId, ct);
        if (facts is null) return false;                                 // unknown resource

        var isInstructor = facts.InstructorId == userId;
        var isAdmin = user.IsInRole(RoleNames.Admin);
        var isModerator = user.IsInRole(RoleNames.Moderator);

        return operation.Name switch
        {
            nameof(CourseOperations.Read) =>
                isInstructor || isAdmin || isModerator || facts is { Enrolled: true, Visible: true },

            nameof(CourseOperations.Edit) or nameof(CourseOperations.Delete) =>
                isInstructor,

            // An instructor cannot re-publish while staff have the course on hold.
            nameof(CourseOperations.Publish) =>
                isInstructor && !facts.ModerationHold,

            // Admins may take content down but never delete it.
            // Add "|| isModerator" if access controls are changed to allow moderators to unpublish too.
            nameof(CourseOperations.Unpublish) =>
                isInstructor || isAdmin,

            _ => false
        };
    }

    // One query per call. Enrollment is a sub-select, so there is no second round trip.
    private async Task<Facts?> LoadAsync(CourseResource resource, Guid userId, CancellationToken ct)
    {
        var id = resource.Id;

        switch (resource.Kind)
        {
            case CourseResourceKind.Course:
                return await db.Courses.AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => new Facts(
                        c.InstructorId,
                        c.Status == CourseStatus.ModerationHold,
                        c.Status == CourseStatus.Published,
                        db.Enrollments.Any(e => e.CourseId == c.Id && e.StudentId == userId)))
                    .SingleOrDefaultAsync(ct);

            case CourseResourceKind.Module:
                return await db.Modules.AsNoTracking()
                    .Where(m => m.Id == id)
                    .Select(m => new Facts(
                        m.Course.InstructorId,
                        m.Course.Status == CourseStatus.ModerationHold
                            || m.Status == ModuleStatus.ModerationHold,
                        m.Course.Status == CourseStatus.Published
                            && m.Status == ModuleStatus.Published,
                        db.Enrollments.Any(e => e.CourseId == m.CourseId && e.StudentId == userId)))
                    .SingleOrDefaultAsync(ct);

            case CourseResourceKind.Lesson:
                return await db.Lessons.AsNoTracking()
                    .Where(l => l.Id == id)
                    .Select(l => new Facts(
                        l.Module.Course.InstructorId,
                        l.Module.Course.Status == CourseStatus.ModerationHold
                            || l.Module.Status == ModuleStatus.ModerationHold
                            || l.Status == LessonStatus.ModerationHold,
                        l.Module.Course.Status == CourseStatus.Published
                            && l.Module.Status == ModuleStatus.Published
                            && l.Status == LessonStatus.Published,
                        db.Enrollments.Any(e => e.CourseId == l.Module.CourseId && e.StudentId == userId)))
                    .SingleOrDefaultAsync(ct);

            case CourseResourceKind.Asset:
                return await db.LessonAssets.AsNoTracking()
                    .Where(a => a.Id == id)
                    .Select(a => new Facts(
                        a.Lesson.Module.Course.InstructorId,
                        a.Lesson.Module.Course.Status == CourseStatus.ModerationHold
                            || a.Lesson.Module.Status == ModuleStatus.ModerationHold
                            || a.Lesson.Status == LessonStatus.ModerationHold,
                        a.Lesson.Module.Course.Status == CourseStatus.Published
                            && a.Lesson.Module.Status == ModuleStatus.Published
                            && a.Lesson.Status == LessonStatus.Published,
                        db.Enrollments.Any(e => e.CourseId == a.Lesson.Module.CourseId && e.StudentId == userId)))
                    .SingleOrDefaultAsync(ct);

            default:
                return null;
        }
    }
}

// --------------------------- route -> resource (for [Authorize(Policy)]) ---------------------

internal static class CourseRouteResolver
{
    // Most specific first. The first key that is present decides; less specific keys are ignored.
    private static readonly (string Key, CourseResourceKind Kind)[] Keys =
    [
        ("assetId",  CourseResourceKind.Asset),
        ("lessonId", CourseResourceKind.Lesson),
        ("moduleId", CourseResourceKind.Module),
        ("courseId", CourseResourceKind.Course),
    ];

    public static bool TryResolve(RouteValueDictionary route, out CourseResource resource)
    {
        foreach (var (key, kind) in Keys)
        {
            if (!route.TryGetValue(key, out var raw)) continue;

            // A malformed id never falls back to a less specific one (no downgrade tricks).
            if (!Guid.TryParse(raw?.ToString(), out var id)) break;

            resource = new CourseResource(kind, id);
            return true;
        }

        resource = default;
        return false;
    }
}

// ------------------------------------- the handler ------------------------------------------

/// <summary>
/// Works two ways:
///  1. Policy on an action:  [Authorize(Policy = PolicyNames.CanReadCourseContent)]
///     The resource comes from the route (assetId, lessonId, moduleId or courseId).
///  2. Resource-based, from a controller or service:
///     await authz.AuthorizeAsync(User, new CourseResource(CourseResourceKind.Asset, id), CourseOperations.Read)
/// </summary>
public sealed class CourseContentHandler(ICourseAccess access)
    : AuthorizationHandler<OperationAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement)
    {
        if (!CourseOperations.IsCourseOperation(requirement)) return;    // not ours

        var ct = CancellationToken.None;
        CourseResource resource;

        switch (context.Resource)
        {
            case CourseResource direct:
                resource = direct;
                break;

            case HttpContext http:
                if (!CourseRouteResolver.TryResolve(http.Request.RouteValues, out resource)) return;
                ct = http.RequestAborted;
                break;

            case AuthorizationFilterContext filter:
                if (!CourseRouteResolver.TryResolve(filter.HttpContext.Request.RouteValues, out resource)) return;
                ct = filter.HttpContext.RequestAborted;
                break;

            default:
                return;
        }

        if (await access.CanAsync(context.User, requirement, resource, ct))
            context.Succeed(requirement);
    }
}

// ----------------------------------- registration -------------------------------------------

public static class CourseAccessExtensions
{
    public static IServiceCollection AddCourseAccessPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.CanReadCourseContent, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(CourseOperations.Read))
            .AddPolicy(PolicyNames.CanEditCourseContent, p => p
                .RequireRole(RoleNames.Instructor)
                .AddRequirements(CourseOperations.Edit))
            .AddPolicy(PolicyNames.CanDeleteCourseContent, p => p
                .RequireRole(RoleNames.Instructor)
                .AddRequirements(CourseOperations.Delete))
            .AddPolicy(PolicyNames.CanPublishCourseContent, p => p
                .RequireRole(RoleNames.Instructor)
                .AddRequirements(CourseOperations.Publish))
            .AddPolicy(PolicyNames.CanUnpublishCourseContent, p => p
                .RequireAuthenticatedUser()                     // instructor or Admin, decided in the handler
                .AddRequirements(CourseOperations.Unpublish));

        services.AddScoped<ICourseAccess, CourseAccess>();
        services.AddScoped<IAuthorizationHandler, CourseContentHandler>();
        return services;
    }
}

/* ---- ----

---- Program.cs ----

builder.Services.AddCourseAccessPolicies();     // replaces AddLessonPolicies()

---- controller usage ----

[HttpGet("lessons/{lessonId:guid}")]
[Authorize(Policy = PolicyNames.CanReadCourseContent)]
public Task<IActionResult> GetLesson(Guid lessonId) { ... }

[HttpDelete("lessons/{lessonId:guid}")]
[Authorize(Policy = PolicyNames.CanDeleteCourseContent)]
public Task<IActionResult> DeleteLesson(Guid lessonId) { ... }

[HttpPost("courses/{courseId:guid}/unpublish")]
[Authorize(Policy = PolicyNames.CanUnpublishCourseContent)]
public Task<IActionResult> Unpublish(Guid courseId) { ... }

---- presigned links (service or controller, no route needed) ----

var result = await authz.AuthorizeAsync(
    User, new CourseResource(CourseResourceKind.Asset, assetId), CourseOperations.Read);
if (!result.Succeeded) return Forbid();

*/