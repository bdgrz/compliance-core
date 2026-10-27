using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Immutable readiness histories at exact hydrated native and physical cuts; never command state.</summary>
public static class ReadinessHistory
{
    /// <summary>Reads all program facts at the captured cut and verifies their original scope and transition lineage.</summary>
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events, ProgramReadiness owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        RequireTail(facts, owner.Latest);
        if (facts.Count != owner.Revision)
            throw Invalid("The program readiness history differs from its hydrated logical cut.");
        var operations = new HashSet<Uuid>();
        var assessments = new HashSet<Uuid>();
        var decided = new HashSet<Uuid>();
        var entries = new HashSet<Uuid>();
        ReadinessAssessmentRequested? run = null;
        ReadinessAssessmentAdopted? adopted = null;
        DomainEvent? pending = null;
        long generation = 0;
        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];
            if (fact is not INativeOperationFact native || native.Operation.LogicalId == Uuid.Empty ||
                !operations.Add(native.Operation.LogicalId))
                throw Invalid("A readiness history repeats an operation or has another native fact type.");
            var operation = native.Operation;
            var revision = index + 1;
            bool valid;
            switch (fact)
            {
                case ReadinessAssessmentRequested requested:
                    valid = run is null && pending is null && requested.Owner == owner.Stream &&
                        requested.Revision == revision && requested.Generation == ++generation &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RunReadinessAssessment), (ulong)index);
                    run = requested;
                    break;
                case ReadinessAssessmentAdopted adoption:
                    valid = run is not null && adoption.Owner == owner.Stream && adoption.Revision == revision &&
                        adoption.Generation == run.Generation && adoption.Intent == run.Intent &&
                        Child(operation, run.Operation, owner.Stream) && assessments.Add(operation.RootOperationId);
                    adopted = adoption;
                    run = null;
                    break;
                case ReadinessDecisionAccepted decisionAccepted:
                    valid = Decision(decisionAccepted.Owner, decisionAccepted.Revision, decisionAccepted.Generation, operation,
                        nameof(DecideReadiness));
                    pending = decisionAccepted;
                    break;
                case TypeIEntryDecisionAccepted entryAccepted:
                    valid = Decision(entryAccepted.Owner, entryAccepted.Revision, entryAccepted.Generation, operation,
                        nameof(DecideTypeIEntry));
                    pending = entryAccepted;
                    break;
                case ReadinessDecisionRecorded recorded:
                    valid = pending is ReadinessDecisionAccepted recordedAcceptance && Scope(recorded.TenantId, recorded.ProgramId) &&
                        recorded.Revision == revision && recorded.AssessmentId == adopted?.Operation.RootOperationId &&
                        recorded.DecisionId == recordedAcceptance.Operation.RootOperationId && recorded.Intent == recordedAcceptance.Intent &&
                        Child(operation, recordedAcceptance.Operation, owner.Stream) && decided.Add(recorded.AssessmentId);
                    pending = null;
                    break;
                case TypeIEntryDecisionRecorded recorded:
                    valid = pending is TypeIEntryDecisionAccepted recordedEntryAcceptance && Scope(recorded.TenantId, recorded.ProgramId) &&
                        recorded.Revision == revision && recorded.AssessmentId == adopted?.Operation.RootOperationId &&
                        recorded.DecisionId == recordedEntryAcceptance.Operation.RootOperationId && recorded.Intent == recordedEntryAcceptance.Intent &&
                        Child(operation, recordedEntryAcceptance.Operation, owner.Stream) && entries.Add(recorded.AssessmentId);
                    pending = null;
                    break;
                case ReadinessDecisionCanceled canceled:
                    var terms = ProgramReadiness.DecisionTerms(pending);
                    valid = pending is INativeOperationFact admission && canceled.Owner == owner.Stream &&
                        canceled.Revision == revision && canceled.Generation == terms?.Generation &&
                        canceled.Intent == terms?.Intent && canceled.SourceProofs is not null &&
                        Child(operation, admission.Operation, owner.Stream);
                    pending = null;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The readiness history has another scope, revision or admitted transition.");

            bool Scope(Uuid tenant, Uuid program) => tenant == owner.TenantId && program == owner.ProgramId;
            bool Decision(EventStreamAddress stream, long logicalRevision, long selectedGeneration,
                OperationProvenance root, string kind) => adopted is not null && run is null && pending is null &&
                stream == owner.Stream && logicalRevision == revision && selectedGeneration == adopted.Generation &&
                NativeOwnerHistory.IsOwnRoot(owner, root, kind, (ulong)index);
        }
        return facts;
    }

    /// <summary>Reads plans, annotations and existing source-use facts without retaining their lifetime collections.</summary>
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events, ReadinessGapPlan owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        RequireTail(facts, owner.LatestFact);
        var operations = new HashSet<Uuid>();
        long revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.LogicalId == Uuid.Empty ||
                !operations.Add(native.Operation.LogicalId))
                throw Invalid("A gap history repeats a logical operation or has another fact type.");
            var valid = fact switch
            {
                ReadinessGapPlanned plan => plan.TenantId == owner.TenantId && plan.ProgramId == owner.ProgramId &&
                    plan.Revision == ++revision && NativeOwnerHistory.IsOwnRoot(owner, plan.Operation,
                        nameof(PlanReadinessGap), (ulong)revision - 1),
                ReadinessGapAnnotated annotation => annotation.TenantId == owner.TenantId &&
                    annotation.ProgramId == owner.ProgramId && annotation.Revision == revision &&
                    NativeOwnerHistory.IsOwnRoot(owner, annotation.Operation, nameof(AnnotateReadinessGap), (ulong)revision),
                ReadinessGapPlanReadinessDecisionUseAuthorized use => use.Owner == owner.Stream &&
                    use.Revision == revision && use.Operation.Owner == owner.Stream,
                ReadinessGapPlanReadinessDecisionUseSettled use => use.Owner == owner.Stream &&
                    use.Revision == revision && use.Operation.Owner == owner.Stream,
                _ => false,
            };
            if (!valid)
                throw Invalid("The gap history has another scope or plan revision.");
        }
        if (revision != owner.Revision)
            throw Invalid("The gap history differs from the current plan revision.");
        return facts;
    }

    static bool Child(OperationProvenance child, OperationProvenance root, EventStreamAddress owner) =>
        child.RootOperationId == root.RootOperationId && child.Owner == owner &&
        (child.FirstOwner ?? owner) == (root.FirstOwner ?? owner) && child.Actor == root.Actor &&
        child.Episode == root.Episode && child.AuthoredIntentSha256 == root.AuthoredIntentSha256 &&
        child.OriginalExpectedRevision == root.OriginalExpectedRevision;

    static void RequireTail(IReadOnlyList<DomainEvent> facts, DomainEvent? latest)
    {
        if (facts.Count > 0 && (latest is null || ContentDigest.Sha256(facts[^1], facts[^1].GetType()) !=
            ContentDigest.Sha256(latest, latest.GetType())))
            throw Invalid("The readiness history body differs from the hydrated native cut.");
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
