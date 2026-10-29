using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.SourceUse.Models;

/// <summary>
///     What a pair verified about its proven source acceptance, derived from the immutable workflow intent. A
///     non-null <paramref name="SupersededReason" /> means the source operation is already terminal and the receiver is
///     a no-op. <paramref name="Disposition" /> is the disposition the verified terminal source event kind requires; a
///     settlement carries it, an authorization does not.
/// </summary>
public sealed record SourceUseSource(DurableIntentHeader Header, ImmutableContentRef ParentIntent,
    ReservationKey Key, SourceUseRequirement Requirement, string CommandKind,
    SourceUseSettlementDisposition? Disposition, string? SupersededReason)
{
    /// <summary>A canceled or rejected source requires the process abort and settlement stages to recover any held uses.</summary>
    public bool RequiresAbort { get; init; }
}
