namespace RondiTrack.Common;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    string NextPageToken,
    int PageSize)
{
    public bool HasMore => !string.IsNullOrEmpty(NextPageToken);
}