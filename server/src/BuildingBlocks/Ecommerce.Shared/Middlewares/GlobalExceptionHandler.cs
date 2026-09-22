using Ecommerce.Shared.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Shared.Middlewares;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;
    private readonly IHostEnvironment _env = env;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, errors) = exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    )
            ),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                null
            ),
            ConflictException => (
                StatusCodes.Status409Conflict,
                "Resource Conflict",
                null
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                null
            ),
            // 503, not 500. A 500 says "we are broken"; this says "a dependency is down, try
            // again shortly", and the difference decides whether a caller retries and whether a
            // monitor pages somebody at 3am.
            DependencyUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                null
            )
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            // A DOMAIN refusal is written for the customer, so it is shown to the customer -
            // "Not sold in USD: Sony A7 IV", "The cart is empty", "Delivery address not found".
            // Hiding those behind "An error occurred" was making every carefully worded refusal in
            // this system invisible the moment it ran outside Development, and FR-003 of specs/022
            // (the refusal must NAME what it refused) was therefore not met in any deployed image.
            //
            // Anything NOT in the list above keeps its message hidden: an unmapped exception is an
            // internal one, and its text can carry a connection string, a file path or a row that
            // nobody outside should see.
            Detail = ShowMessage(exception) || _env.IsDevelopment()
                ? exception.Message
                : "An error occurred while processing your request.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (errors != null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    /// <summary>
    /// Whether this exception's message was written to be read by whoever made the request.
    /// </summary>
    /// <remarks>
    /// Only the types this handler maps deliberately. They are thrown by handlers with a sentence a
    /// customer can act on; everything else is internal and keeps its message to itself.
    /// </remarks>
    private static bool ShowMessage(Exception exception) => exception
        is ValidationException
        or NotFoundException
        or ConflictException
        or DependencyUnavailableException;
}
