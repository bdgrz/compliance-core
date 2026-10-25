namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Raised when a source fact is missing or a proof does not match the stored record.</summary>
public class SourceFactVerificationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SourceFactVerificationException(string message) : base(message)
    {
    }
}
