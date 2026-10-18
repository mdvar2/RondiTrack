
namespace RondiTrack.BackgroundJobs;

public sealed class CycleCloseOptions
{
    public const string SectionName = "CycleClose";

    public int IntervalSeconds { get; set; } = 60;
}
