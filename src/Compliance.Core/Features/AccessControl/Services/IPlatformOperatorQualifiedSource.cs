using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Opens an exact qualified participant source read, or returns null if unavailable.</summary>
/// <remarks>There is no granting default implementation. Each session must bind every returned
/// record and its exclusive head to one source incarnation and retained boundary.</remarks>
public interface IPlatformOperatorQualifiedSource
{
    /// <summary>Opens one exact source session without treating absence as qualification.</summary>
    ValueTask<IPlatformOperatorQualifiedSourceSession?> OpenAsync(EventStreamAddress stream, Uuid aggregateId,
        CancellationToken ct = default);
}
