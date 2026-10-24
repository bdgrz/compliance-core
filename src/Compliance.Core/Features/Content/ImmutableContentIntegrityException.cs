namespace Bdgrz.Compliance.Features.Content;

/// <summary>Raised when stored content is missing, corrupt, or does not match the reference used to read it.</summary>
public class ImmutableContentIntegrityException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ImmutableContentIntegrityException(string message) : base(message)
    {
    }
}
