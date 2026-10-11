using RondiTrack.DTOs.Contributions;

namespace RondiTrack.Idempotency;

public class IdempotencyRecord
{
    public string Key { get; }
    public string RequestHash { get; }
    public int ResponseStatus { get; }
    public ContributionResponse ResponseBody { get; }
    public DateTime CreatedAtUtc { get; }

    public IdempotencyRecord(
        string key,
        string requestHash,
        int responseStatus,
        ContributionResponse responseBody)
    {
        Key = key;
        RequestHash = requestHash;
        ResponseStatus = responseStatus;
        ResponseBody = responseBody;
        CreatedAtUtc = DateTime.UtcNow;
    }
}