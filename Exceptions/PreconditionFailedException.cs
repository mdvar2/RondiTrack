namespace RondiTrack.Exceptions;

public class PreconditionFailedException : RondiTrackException
{
    public PreconditionFailedException(string message)
        : base(message)
    {
    }
}
