using Microsoft.AspNetCore.Mvc.Filters;

namespace VehicleManagement.Logging;

/// <summary>Logs rejected model fields without recording submitted values.</summary>
public sealed class ValidationLoggingFilter : IAsyncActionFilter
{
    private readonly ILogger<ValidationLoggingFilter> _logger;

    public ValidationLoggingFilter(ILogger<ValidationLoggingFilter> logger) => _logger = logger;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var fields = string.Join(", ", context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0).Select(entry => entry.Key));
            _logger.LogWarning("Input validation rejected {Action}. Invalid fields: {Fields}. Reference {RequestId}",
                context.ActionDescriptor.DisplayName, fields, context.HttpContext.TraceIdentifier);
        }
        await next();
    }
}
