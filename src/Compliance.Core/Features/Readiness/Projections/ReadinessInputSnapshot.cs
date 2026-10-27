using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Readiness.Projections;

/// <summary>
///     The complete typed inputs of one readiness evaluation, frozen once when the run is admitted. Each large part is
///     verified immutable content, so the computation, a replay and a rerun read exactly these inputs and no live
///     source. <paramref name="SourceCuts" /> lists the authoritative read cuts the source scan captured.
/// </summary>
public sealed record ReadinessInputSnapshot(Uuid ProgramId, DateTimeOffset AsOf, Uuid? EditionId,
    ImmutableContentRef? CatalogEdition, ImmutableContentRef Criteria, ImmutableContentRef Mappings,
    ImmutableContentRef EffectiveControls, ImmutableContentRef Sources, ImmutableSelectionManifest SourceCuts)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "readiness-input-snapshot.v1";
}
