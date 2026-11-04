namespace RondiTrack.Domain.Exceptions;

public sealed class ExpiredException(string message) : RondiTrackException(message);
