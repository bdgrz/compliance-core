namespace Bdgrz.Compliance.Features.Content;

/// <summary>Stored content has not become readable yet; durable receivers keep the operation pending.</summary>
public sealed class ImmutableContentUnavailableException(string message) : ImmutableContentIntegrityException(message);
