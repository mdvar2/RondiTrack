using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Exceptions;

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
            ForbiddenException =>
                (StatusCodes.Status403Forbidden, "Forbidden"),

            AuthenticationFailedException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized"),

            RequestValidationException =>
                (StatusCodes.Status400BadRequest, "Bad Request"),

            NotFoundException =>
                (StatusCodes.Status404NotFound, "Not Found"),

            PreconditionFailedException =>
                (StatusCodes.Status412PreconditionFailed,
                    "Precondition Failed"),

            BusinessRuleException =>
                (StatusCodes.Status409Conflict, "Conflict"),

            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Conflict"),

            DbUpdateException dbException when IsUniqueConstraintViolation(dbException) =>
                (StatusCodes.Status409Conflict, "Conflict"),

            _ =>
                (StatusCodes.Status500InternalServerError,
                    "Internal Server Error")
        };

        if (exception is AuthenticationFailedException)
            _logger.LogInformation("Authentication rejected. CorrelationId: {CorrelationId}", correlationId);
        else
            _logger.LogError(exception, "Request failed. CorrelationId: {CorrelationId}", correlationId);

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = statusCode,
            Detail = exception is RondiTrackException
                ? exception.Message
                : exception is DbUpdateException && IsUniqueConstraintViolation((DbUpdateException)exception)
                    ? "A database constraint was violated."
                    : "An unexpected error occurred.",
            Instance = httpContext.Request.Path
        };

        // Login failures must be identical for unknown accounts and wrong passwords.
        if (exception is not AuthenticationFailedException)
            problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode =
            statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        var inner = exception.InnerException;

        while (inner is not null)
        {
            var message = inner.Message ?? string.Empty;
            if (message.Contains("23505", StringComparison.Ordinal) ||
                message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            inner = inner.InnerException;
        }

        return false;
    }
}