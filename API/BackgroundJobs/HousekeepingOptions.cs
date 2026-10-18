namespace RondiTrack.BackgroundJobs;

public sealed class HousekeepingOptions
{
    public const string SectionName = "Housekeeping";
    public int IntervalHours { get; set; } = 24;
    public int RefreshTokenGraceDays { get; set; } = 7;
    public int IdempotencyRetentionHours { get; set; } = 24;
}
