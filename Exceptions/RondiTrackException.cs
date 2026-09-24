namespace RondiTrack.Exceptions;

public abstract class RondiTrackException : Exception
{
    protected RondiTrackException(string message)
        : base(message)
    {
    }
}