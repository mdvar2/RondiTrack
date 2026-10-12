using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Observability;

public static class Correlation
{
    public static string GetId(HttpContext context)
    {
        // Prefer the request activity even when called inside a child activity.
        var activity = context.Features.Get<IHttpActivityFeature>()?.Activity ?? Activity.Current;
        if (activity is null || activity.IdFormat != ActivityIdFormat.W3C)
            throw new InvalidOperationException("A W3C request activity is required for error correlation.");
        return activity.TraceId.ToString();
    }

    public static void Apply(HttpContext context, ProblemDetails problem) =>
        problem.Extensions["correlationId"] = GetId(context);
}
