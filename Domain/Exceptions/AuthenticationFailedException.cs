namespace RondiTrack.Domain.Exceptions;

public class AuthenticationFailedException(string message) : RondiTrackException(message);
