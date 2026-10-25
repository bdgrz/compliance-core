namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>The exact native source record is not visible yet; durable source consumers may retry.</summary>
public sealed class SourceFactUnavailableException(string message) : SourceFactVerificationException(message);
