namespace RondiTrack.Exceptions;

public class NotFoundException : RondiTrackException
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}