namespace RondiTrack.Exceptions;

public class BusinessRuleException : RondiTrackException
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}