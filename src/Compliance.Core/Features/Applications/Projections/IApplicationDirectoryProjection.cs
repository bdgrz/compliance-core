using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Projections;

public interface IApplicationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);

    /// <summary>
    ///     Projects the published candidates of one committed batch from its complete frozen plan, its frozen rows and
    ///     the native proof of every row.
    /// </summary>
    ValueTask ApplyCommittedImportAsync(ApplicationImportCommitted marker, ImportFrozenPlanIntent plan,
        IReadOnlyList<ApplicationImportPlannedRow> rows, IReadOnlyList<ApplicationImportEffectProof> proofs,
        CancellationToken ct = default);
}
