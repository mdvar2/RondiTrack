using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Exceptions;

namespace RondiTrack.Handlers;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId =
            httpContext.TraceIdentifier;

        var (statusCode, title) = exception switch
        {
            RequestValidationException =>
                (StatusCodes.Status400BadRequest, "Bad Request"),

             NotFoundException =>
                (StatusCodes.Status404NotFound, "Not Found"),

            BusinessRuleException =>
                (StatusCodes.Status409Conflict, "Conflict"),

            _ =>
                (StatusCodes.Status500InternalServerError,
                    "Internal Server Error")
        };

        _logger.LogError(
            exception,
            "Request failed. CorrelationId: {CorrelationId}",
            correlationId);

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = statusCode,
            Detail = exception is RondiTrackException
                ? exception.Message
                : "An unexpected error occurred.",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["correlationId"] =
            correlationId;

        httpContext.Response.StatusCode =
            statusCode;

        httpContext.Response.ContentType =
            "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            cancellationToken);

        return true;
    }
}