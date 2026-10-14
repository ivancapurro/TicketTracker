using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TicketTracker.Api.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ApiException apiException => (apiException.StatusCode, apiException.Title, apiException.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "The request could not be completed."),
        };

        if (exception is not ApiException)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = status;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken);
        }

        return true;
    }
}
