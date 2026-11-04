namespace RondiTrack.Domain.Entities;

public sealed class ContributionProof
{
    public Guid Id { get; private set; }
    public Guid ContributionId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long Length { get; private set; }
    public byte[] Contents { get; private set; } = [];
    public DateTime UploadedAtUtc { get; private set; }
    public Contribution Contribution { get; private set; } = null!;

    private ContributionProof() { }

    public ContributionProof(Guid contributionId, string fileName, string contentType, byte[] contents)
    {
        Id = Guid.NewGuid();
        ContributionId = contributionId;
        FileName = Path.GetFileName(fileName);
        ContentType = contentType;
        Contents = contents;
        Length = contents.LongLength;
        UploadedAtUtc = DateTime.UtcNow;
    }
}
