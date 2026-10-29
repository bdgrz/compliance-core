using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>
///     The projected state of one mapping line: <paramref name="LineId" /> is the identity of its owner stream and
///     <paramref name="PendingMappingId" /> the id of the proposal awaiting review, if any.
/// </summary>
public sealed record ControlCriterionMappingWorkState(Uuid TenantId, Uuid ProgramId, Uuid LineId, Uuid ControlId,
    Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier, string CriterionKind, long Revision,
    int LastVersionNumber, int? ActiveVersionNumber, int? PendingVersionNumber, Uuid? PendingMappingId,
    Uuid? ProposerMemberId, DateTimeOffset? ProposedAt, string State);
