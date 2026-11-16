// Filters/ValidateCsrfFilter.cs
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpenAcademy.Responses;

namespace OpenAcademy.Filters;

public sealed class ValidateCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method))
            return;

        // Honor [IgnoreAntiforgeryToken] on actions/controllers
        if (context.Filters.OfType<IAntiforgeryPolicy>().Any()
            || context.ActionDescriptor.EndpointMetadata.OfType<IAntiforgeryMetadata>()
                .Any(m => !m.RequiresValidation))
            return;

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Failed(
                code: "CSRF_VALIDATION_FAILURE",
                message: "The CSRF token is missing or invalid."));
        }
    }
}