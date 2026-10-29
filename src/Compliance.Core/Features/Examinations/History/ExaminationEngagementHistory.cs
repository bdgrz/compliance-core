using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Projections;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.History;

/// <summary>Verifies every actual source fact, including unselected history, at the exact hydrated cut.</summary>
public static class ExaminationEngagementHistory
{
    public static async Task<ExaminationEngagementHistorySnapshot> ReadAsync(IDomainEventReader events,
        ExaminationEngagement owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var operations = new HashSet<(Uuid Id, string Kind)>();
        var baselines = new Dictionary<Uuid, ImmutableContentRef>();
        var digest = ExaminationEngagementReplayDigest.Empty;
        ExaminationEngagementHeader? header = null;
        ImmutableContentRef? current = null;
        ImmutableContentRef? initial = null;
        ImmutableContentRef? baseline = null;
        long revision = 0;
        foreach (var domainEvent in facts)
        {
            if (domainEvent is not IExaminationEngagementFact fact || fact.TenantId != owner.TenantId ||
                fact.SubjectId != owner.Id || fact.Revision != ++revision || fact.ProgramId == Uuid.Empty ||
                fact.Change.Schema != ExaminationEngagementState.Schema || fact.FirstResponse is null ||
                fact.OperationResult is null || fact.OccurredAt != fact.Operation.AcceptedAt ||
                fact.ActorMemberId != ExaminationEngagement.Member(fact.Operation) ||
                !owner.OwnRoot(fact.Operation, ExaminationEngagement.Kind(domainEvent), revision == 1 ? null : (ulong)(revision - 1)) ||
                !operations.Add((fact.Operation.LogicalId, fact.Operation.CommandKind)))
                throw Invalid("The native engagement history differs from its exact scope, root, actor or unique operation.");
            if (header is null)
            {
                if (domainEvent is not ExaminationEngagementOpened || fact.Operation.LogicalId != owner.Id ||
                    fact.EngagementType != "type_i" || fact.BoundaryId == Uuid.Empty ||
                    fact.BaselineId is not null || fact.BaselineRevision != 0)
                    throw Invalid("The native engagement history must begin at its own exact initial identity.");
                initial = fact.Change;
            }
            else if (domainEvent is ExaminationEngagementOpened || fact.ProgramId != header.ProgramId ||
                fact.EngagementType != header.EngagementType || fact.BoundaryId != header.BoundaryId ||
                domainEvent is ExaminationEngagementRevised && (header.BaselineId is not null ||
                    fact.BaselineId is not null || fact.BaselineRevision != 0) ||
                domainEvent is TypeIBaselineFrozen && (header.BaselineId is not null || fact.BaselineRevision != 1) ||
                domainEvent is TypeIBaselineAmended && (header.BaselineId is null ||
                    fact.BaselineRevision != header.BaselineRevision + 1))
                throw Invalid("The native engagement history has contradictory lifecycle or baseline lineage.");
            var frozen = domainEvent is TypeIBaselineFrozen or TypeIBaselineAmended;
            if (frozen)
            {
                if (fact.BaselineId is not { } id || id == Uuid.Empty || fact.Operation.LogicalId != id ||
                    !baselines.TryAdd(id, fact.Change))
                    throw Invalid("A baseline history must introduce one exact immutable baseline identity per root.");
                baseline = fact.Change;
            }
            header = new ExaminationEngagementHeader(fact.ProgramId, fact.EngagementType, fact.BoundaryId,
                frozen ? "baseline_frozen" : "draft", fact.Revision, fact.BaselineId, fact.BaselineRevision);
            current = fact.Change;
            if (fact.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedCommandKind is null || owner.Acceptance.SelectedCommandKind == fact.Operation.CommandKind) &&
                (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(domainEvent) || selected.Metadata != domainEvent.Metadata))
                throw Invalid("The selected acceptance differs from its exact raw payload and metadata.");
            digest = ExaminationEngagementReplayDigest.Append(digest, domainEvent);
        }
        if (header != owner.Header || current != owner.CurrentContent || initial != owner.InitialContent ||
            baseline != owner.BaselineContent || owner.Revision != revision || owner.SelectedBaselineId != Uuid.Empty &&
            owner.SelectedBaselineContent != baselines.GetValueOrDefault(owner.SelectedBaselineId))
            throw Invalid("The composed engagement history differs from the actual hydrated authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new ExaminationEngagementHistorySnapshot(owner, facts, baselines);
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
