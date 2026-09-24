namespace RondiTrack.Exceptions;

public class RequestValidationException
    : RondiTrackException
{
    public RequestValidationException(string message)
        : base(message)
    {
    }
}