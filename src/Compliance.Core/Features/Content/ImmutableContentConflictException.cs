namespace Bdgrz.Compliance.Features.Content;

/// <summary>Raised when a content id is written again with different content.</summary>
public sealed class ImmutableContentConflictException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ImmutableContentConflictException(string message) : base(message)
    {
    }
}
