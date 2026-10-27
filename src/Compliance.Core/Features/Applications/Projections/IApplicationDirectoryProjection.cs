using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
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
