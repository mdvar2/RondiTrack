namespace RondiTrack.Domain.Entities;

public sealed class PersistedIdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
