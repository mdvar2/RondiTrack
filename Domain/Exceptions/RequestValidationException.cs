namespace RondiTrack.Domain.Exceptions;

public class RequestValidationException
    : RondiTrackException
{
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public RequestValidationException(string message)
        : base(message)
    {
    }

    public RequestValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message) => Errors = errors;
}
