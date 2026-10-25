using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Transient complete validation at the exact hydrated native cut; historical text is not needed.</summary>
static class AdvisorNativeHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(Aggregate owner, IDomainEventReader events,
        CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var (acceptance, cut) = owner switch
        {
            AdvisorHandoff handoff => (handoff.Acceptance, handoff.ReplayCut),
            AdvisorFeedbackThread thread => (thread.Acceptance, thread.ReplayCut),
            _ => throw new ArgumentException("The owner is not an advisor handoff or feedback thread.", nameof(owner)),
        };
        var digest = AdvisorReplayDigest.Empty;
        var logicalIds = new HashSet<Uuid>();
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact { Operation: { } operation } || operation.Owner != owner.Stream ||
                operation.LogicalId == Uuid.Empty || !logicalIds.Add(operation.LogicalId))
                throw Invalid("The advisor history has another owner or a duplicate logical operation.");
            digest = AdvisorReplayDigest.Append(digest, fact);
            if (operation.LogicalId == acceptance.SelectedLogicalId &&
                (acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected advisor acceptance differs from its actual native payload and complete metadata.");
        }
        if (owner is AdvisorHandoff handoffOwner)
            Handoff(handoffOwner, facts);
        else
            Thread((AdvisorFeedbackThread)owner, facts);
        cut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return facts;
    }

    static void Handoff(AdvisorHandoff owner, IReadOnlyList<DomainEvent> facts)
    {
        AdvisorHandoffCreated? created = null;
        ImmutableContentRef? manifest = null;
        var status = "missing";
        var revision = 0L;
        var generation = 0L;
        var admissions = 0L;
        var ready = false;
        DateTimeOffset? updatedAt = null;
        var admittedIds = new HashSet<Uuid>();
        foreach (var fact in facts)
        {
            var open = status is AdvisorHandoff.Draft or AdvisorHandoff.ReadyForReview;
            switch (fact)
            {
                case AdvisorHandoffCreated value:
                    if (created is not null || value.TenantId != owner.TenantId || value.ProgramId == Uuid.Empty ||
                        value.HandoffId != owner.Id || value.Operation.LogicalId != owner.Id ||
                        !Root(owner, value, nameof(CreateAdvisorHandoff), null, value.CreatedBy, value.CreatedAt) ||
                        !Reference(value.RecordVersionsManifest, AdvisorContent.CreatedSelectionSchema) ||
                        !Reference(value.Authored, AdvisorAuthored.Schema<CreateAdvisorHandoff>()) ||
                        value.ComparedToHandoffId is { } compared && (compared == Uuid.Empty || compared == owner.Id) ||
                        value.ExpiresOn < DateOnly.FromDateTime(value.CreatedAt.UtcDateTime))
                        throw Invalid("An advisor handoff must begin with its exact native creation terms.");
                    created = value;
                    manifest = value.RecordVersionsManifest;
                    revision = 1;
                    generation = 1;
                    status = AdvisorHandoff.Draft;
                    updatedAt = value.CreatedAt;
                    break;
                case AdvisorHandoffSelectionRevised value:
                    if (!Scope(created, value.TenantId, value.ProgramId, value.HandoffId) || !open ||
                        value.Revision != revision + 1 || !Root(owner, value, nameof(ReviseAdvisorHandoffSelection),
                            (ulong)revision, value.RevisedBy, value.RevisedAt) ||
                        !Reference(value.RecordVersionsManifest, AdvisorContent.RevisedSelectionSchema) ||
                        !Reference(value.Authored, AdvisorAuthored.Schema<ReviseAdvisorHandoffSelection>()))
                        throw Invalid("A selection revision must follow its own open handoff and exact original revision.");
                    manifest = value.RecordVersionsManifest;
                    generation++;
                    status = AdvisorHandoff.Draft;
                    revision++;
                    updatedAt = value.RevisedAt;
                    break;
                case AdvisorHandoffReadiedForReview value:
                    if (!Scope(created, value.TenantId, value.ProgramId, value.HandoffId) || status != AdvisorHandoff.Draft ||
                        value.Revision != revision + 1 || !Root(owner, value, nameof(MarkAdvisorHandoffReadyForReview),
                            (ulong)revision, value.ReadiedBy, value.ReadiedAt) ||
                        created!.ExpiresOn < DateOnly.FromDateTime(value.ReadiedAt.UtcDateTime))
                        throw Invalid("Only the exact unexpired draft handoff can become ready for review.");
                    status = AdvisorHandoff.ReadyForReview;
                    ready = true;
                    revision++;
                    updatedAt = value.ReadiedAt;
                    break;
                case AdvisorHandoffClosed value:
                    if (!Scope(created, value.TenantId, value.ProgramId, value.HandoffId) || !open ||
                        value.Revision != revision + 1 || !Root(owner, value, nameof(CloseAdvisorHandoff),
                            (ulong)revision, value.ClosedBy, value.ClosedAt) ||
                        !Reference(value.Authored, AdvisorAuthored.Schema<CloseAdvisorHandoff>()))
                        throw Invalid("Only the exact open handoff can close.");
                    status = AdvisorHandoff.Closed;
                    revision++;
                    updatedAt = value.ClosedAt;
                    break;
                case AdvisorHandoffRevoked value:
                    if (!Scope(created, value.TenantId, value.ProgramId, value.HandoffId) || !open ||
                        value.Revision != revision + 1 || !Root(owner, value, nameof(RevokeAdvisorHandoff),
                            (ulong)revision, value.RevokedBy, value.RevokedAt) ||
                        !Reference(value.Authored, AdvisorAuthored.Schema<RevokeAdvisorHandoff>()))
                        throw Invalid("Only the exact open handoff can be revoked.");
                    status = AdvisorHandoff.Revoked;
                    revision++;
                    updatedAt = value.RevokedAt;
                    break;
                case AdvisorFeedbackAdmitted value:
                    if (created is null || status != AdvisorHandoff.ReadyForReview || value.Owner != owner.Stream ||
                        value.Revision != revision + 1 || value.Generation != generation ||
                        !OwnRoot(owner, value.Operation, nameof(RecordAdvisorFeedback), (ulong)revision) ||
                        !AdvisorHandoffRules.ValidActor(value.Operation.Actor) ||
                        !Reference(value.FirstResponse, FirstResponses.Schema) ||
                        !Reference(value.Intent, ChildAdmissionIntent.Schema) || !admittedIds.Add(value.Operation.LogicalId) ||
                        created.ExpiresOn < DateOnly.FromDateTime(value.Operation.AcceptedAt.UtcDateTime))
                        throw Invalid("Feedback is admitted once at the exact ready, unexpired handoff generation.");
                    admissions++;
                    revision++;
                    updatedAt = value.Operation.AcceptedAt;
                    break;
                default:
                    throw Invalid("The advisor handoff history contains another native fact family.");
            }
        }
        if (owner.IsCreated != (created is not null) || owner.Revision != revision || owner.Generation != generation ||
            owner.AdmissionCount != admissions || owner.Status != status || owner.UpdatedAt != updatedAt ||
            owner.WasReadyForReview != ready || owner.SelectionManifest != manifest ||
            created is not null && (owner.ProgramId != created.ProgramId || owner.CreatedFact is not { } actual ||
                !actual.Equals(created) || actual.Metadata != created.Metadata))
            throw Invalid("The complete native handoff history differs from the hydrated current authority.");
    }

    static void Thread(AdvisorFeedbackThread owner, IReadOnlyList<DomainEvent> facts)
    {
        AdvisorFeedbackRecorded? recorded = null;
        AdvisorFeedbackResolved? resolved = null;
        var revision = 0L;
        foreach (var fact in facts)
        {
            switch (fact)
            {
                case AdvisorFeedbackRecorded value:
                    var parent = new AdvisorHandoff(owner.TenantId, owner.ProgramId, owner.HandoffId).Stream;
                    var operation = value.Operation;
                    if (recorded is not null || !Scope(owner, value.TenantId, value.ProgramId, value.HandoffId, value.FeedbackId) ||
                        value.RecordId == Uuid.Empty || value.RecordVersionId == Uuid.Empty && value.SelectedSource is null ||
                        value.SelectedSource is { } source && (!source.IsSupported || !source.HasValidIdentity ||
                            source.RecordId != value.RecordId || source.VersionId != value.RecordVersionId) ||
                        !AdvisorHandoffRules.FeedbackKinds.Contains(value.Kind) || !AdvisorHandoffRules.ValidActor(value.Author) ||
                        value.CreatedAt == default || !Reference(value.Intent, ChildAdmissionIntent.Schema) ||
                        operation.Owner != owner.Stream || operation.AffectedOwner != owner.Stream ||
                        operation.ExpectedRevisionOwner != owner.Stream || (operation.FirstOwner ?? operation.Owner) != parent ||
                        operation.RootOperationId != owner.FeedbackId || (operation.CausationEventId is null || operation.CausationEventId == Uuid.Empty) ||
                        operation.CommandKind != nameof(MaterializeAdvisorFeedback) || operation.Episode != AdvisorContent.InitialEpisode ||
                        operation.Actor != value.Author || operation.AcceptedAt == default ||
                        operation.AuthoredIntentSha256 is not { Length: 64 } || !operation.AuthoredIntentSha256.All(Uri.IsHexDigit))
                        throw Invalid("The materialized thread must retain its exact admitted root, target and original contribution metadata.");
                    recorded = value;
                    revision = 1;
                    break;
                case AdvisorFeedbackCorrected value:
                    if (recorded is null || !Scope(owner, value.TenantId, value.ProgramId, value.HandoffId, value.FeedbackId) ||
                        value.CorrectionId != value.Operation.LogicalId ||
                        !Root(owner, value, nameof(CorrectAdvisorFeedback), (ulong)revision, value.CorrectedBy, value.CorrectedAt) ||
                        !Reference(value.Authored, AdvisorAuthored.Schema<CorrectAdvisorFeedback>()))
                        throw Invalid("A correction must follow its exact materialized feedback and original thread revision.");
                    revision++;
                    break;
                case AdvisorFeedbackResponded value:
                    if (recorded is null || !Scope(owner, value.TenantId, value.ProgramId, value.HandoffId, value.FeedbackId) ||
                        !Root(owner, value, nameof(RespondToAdvisorFeedback), (ulong)revision, value.RespondedBy, value.RespondedAt) ||
                        !Reference(value.Authored, AdvisorAuthored.Schema<RespondToAdvisorFeedback>()))
                        throw Invalid("A response must follow its exact materialized feedback and original thread revision.");
                    revision++;
                    break;
                case AdvisorFeedbackResolved value:
                    if (recorded is null || resolved is not null || !Scope(owner, value.TenantId, value.ProgramId, value.HandoffId, value.FeedbackId) ||
                        !Root(owner, value, nameof(ResolveAdvisorFeedback), (ulong)revision, value.ResolvedBy, value.ResolvedAt))
                        throw Invalid("A feedback thread is resolved once at its exact original revision.");
                    resolved = value;
                    revision++;
                    break;
                default:
                    throw Invalid("The advisor feedback history contains another native fact family.");
            }
        }
        if (owner.Revision != revision || !Same(owner.Recorded, recorded) || !Same(owner.Resolved, resolved))
            throw Invalid("The complete native thread history differs from the hydrated current authority.");
    }

    static bool Same(DomainEvent? left, DomainEvent? right) => left is null ? right is null :
        right is not null && left.Equals(right) && left.Metadata == right.Metadata;
    static bool Scope(AdvisorHandoffCreated? created, Uuid tenant, Uuid program, Uuid handoff) => created is not null &&
        tenant == created.TenantId && program == created.ProgramId && handoff == created.HandoffId;
    static bool Scope(AdvisorFeedbackThread owner, Uuid tenant, Uuid program, Uuid handoff, Uuid feedback) =>
        tenant == owner.TenantId && program == owner.ProgramId && handoff == owner.HandoffId && feedback == owner.FeedbackId;
    static bool Root(Aggregate owner, INativeOperationFact fact, string kind, ulong? expected,
        ActorReference actor, DateTimeOffset at) => OwnRoot(owner, fact.Operation, kind, expected) &&
        fact.Operation.Actor == actor && fact.Operation.AcceptedAt == at && AdvisorHandoffRules.ValidActor(actor) &&
        Reference(fact.FirstResponse, FirstResponses.Schema) && Reference(fact switch
        {
            AdvisorHandoffCreated value => value.OperationResult,
            AdvisorHandoffSelectionRevised value => value.OperationResult,
            AdvisorHandoffReadiedForReview value => value.OperationResult,
            AdvisorHandoffClosed value => value.OperationResult,
            AdvisorHandoffRevoked value => value.OperationResult,
            AdvisorFeedbackCorrected value => value.OperationResult,
            AdvisorFeedbackResponded value => value.OperationResult,
            AdvisorFeedbackResolved value => value.OperationResult,
            _ => null,
        }, "mutation-result-content.v1");
    static bool OwnRoot(Aggregate owner, OperationProvenance operation, string kind, ulong? expected) =>
        operation.AuthoredIntentSha256 is { Length: 64 } digest && digest.All(Uri.IsHexDigit) &&
        NativeOwnerHistory.IsOwnRoot(owner, operation, kind, expected);
    internal static bool Reference(ImmutableContentRef? value, string schema) => value is
    { ContentId: var id, Sha256: { Length: 64 } digest, Bytes: > 0, MediaType: "application/json" } &&
        id != Uuid.Empty && digest.All(Uri.IsHexDigit) && value.Schema == schema;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
