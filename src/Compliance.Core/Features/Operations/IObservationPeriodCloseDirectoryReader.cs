using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Bounded identity discovery only; a locator never establishes global mutation authority.</summary>
public interface IObservationPeriodCloseDirectoryReader
{
    ValueTask<IReadOnlyList<ObservationPeriodCloseIdentity>> FindAsync(Uuid tenantId, Uuid programId, Uuid closeId,
        CancellationToken ct = default);
}
