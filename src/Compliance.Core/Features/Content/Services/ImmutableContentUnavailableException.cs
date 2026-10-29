using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
namespace Bdgrz.Compliance.Features.Content.Services;

/// <summary>Stored content has not become readable yet; durable receivers keep the operation pending.</summary>
public sealed class ImmutableContentUnavailableException(string message) : ImmutableContentIntegrityException(message);
