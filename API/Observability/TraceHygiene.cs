using System.Diagnostics;
using OpenTelemetry;

namespace RondiTrack.Observability;

// Attributes that identify infrastructure without helping anyone diagnose a
// failure. Database spans keep db.system.name, db.namespace and
// server.address/port; the connection string (host, database, username) and
// connection ids never reach a trace backend.
public static class TraceHygiene
{
    public static readonly string[] DatabaseDetails = ["db.npgsql.data_source", "db.npgsql.connection_id"];

    public static void StripDatabaseDetails(Activity activity)
    {
        if (activity.Source.Name != "Npgsql") return;
        foreach (var tag in DatabaseDetails)
            activity.SetTag(tag, null);
    }
}

// Runs in the tracing pipeline, at start (tags given when the span is created)
// and at end (tags Npgsql adds later), before export.
public sealed class TraceHygieneProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data) => TraceHygiene.StripDatabaseDetails(data);
    public override void OnEnd(Activity data) => TraceHygiene.StripDatabaseDetails(data);
}
