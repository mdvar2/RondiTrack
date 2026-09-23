using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Common;

public static class ProblemResponses
{
    public static ObjectResult BadRequest(
        string detail,
        string instance)
    {
        return CreateProblem(
            StatusCodes.Status400BadRequest,
            "Bad Request",
            detail,
            instance);
    }

    public static ObjectResult NotFound(
        string detail,
        string instance)
    {
        return CreateProblem(
            StatusCodes.Status404NotFound,
            "Not Found",
            detail,
            instance);
    }

    public static ObjectResult Conflict(
        string detail,
        string instance)
    {
        return CreateProblem(
            StatusCodes.Status409Conflict,
            "Conflict",
            detail,
            instance);
    }

    public static ObjectResult UnprocessableEntity(
        string detail,
        string instance)
    {
        return CreateProblem(
            StatusCodes.Status422UnprocessableEntity,
            "Unprocessable Entity",
            detail,
            instance);
    }

    private static ObjectResult CreateProblem(
        int status,
        string title,
        string detail,
        string instance)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = instance
        };

        var result = new ObjectResult(problem)
        {
            StatusCode = status
        };

        result.ContentTypes.Add("application/problem+json");

        return result;
    }
}