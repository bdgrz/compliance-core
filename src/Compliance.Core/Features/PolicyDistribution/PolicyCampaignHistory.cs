using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Complete transient native validation and explicit selection at the actual hydrated owner cut.</summary>
static class PolicyCampaignHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(Aggregate owner, IDomainEventReader events,
        CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var (acceptance, cut) = owner switch
        {
            PolicyDistributionCampaign campaign => (campaign.Acceptance, campaign.ReplayCut),
            PolicyCampaignParticipant participant => (participant.Acceptance, participant.ReplayCut),
            _ => throw new ArgumentException("The owner is not a policy campaign or participant.", nameof(owner)),
        };
        var digest = PolicyCampaignReplayDigest.Empty;
        var logicalIds = new HashSet<Uuid>();
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact { Operation: { } operation } || operation.Owner != owner.Stream ||
                operation.LogicalId == Uuid.Empty || operation.RootOperationId == Uuid.Empty ||
                !logicalIds.Add(operation.LogicalId) || !ValidActor(operation.Actor) ||
                operation.AuthoredIntentSha256 is not { Length: 64 } sha || !sha.All(Uri.IsHexDigit) ||
                string.IsNullOrWhiteSpace(operation.Episode) || operation.AcceptedAt == default)
                throw Invalid("The campaign history has another owner, malformed native header or duplicate logical operation.");
            digest = PolicyCampaignReplayDigest.Append(digest, fact);
            if (operation.LogicalId == acceptance.SelectedLogicalId &&
                (acceptance.SelectedFact is not { } selected || !Same(selected, fact)))
                throw Invalid("The selected campaign acceptance differs from its actual native payload and complete metadata.");
        }
        if (cut.Digest != digest)
            throw Invalid("The campaign native history differs from the actual hydrated payload and immutable metadata prefix.");
        if (owner is PolicyDistributionCampaign campaignOwner)
            Campaign(campaignOwner, facts);
        else
            Participant((PolicyCampaignParticipant)owner, facts);
        cut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return facts;
    }

    static void Campaign(PolicyDistributionCampaign owner, IReadOnlyList<DomainEvent> facts)
    {
        PolicyCampaignLaunched? launch = null;
        PolicyCampaignAudienceFrozen? frozen = null;
        PolicyCampaignClosureAccepted? pending = null;
        PolicyCampaignClosed? closing = null;
        var closures = new Dictionary<Uuid, PolicyCampaignClosureAccepted>();
        var canceled = new Dictionary<Uuid, PolicyCampaignClosureCanceled>();
        var snapshots = new HashSet<Uuid>();
        var revision = 0L;
        var reconciliation = 0L;
        var generation = 1L;
        var admissions = 0L;
        var latestRoster = Uuid.Empty;
        var index = 0;
        foreach (var fact in facts)
        {
            var currentIndex = index++;
            var open = frozen is not null && pending is null && closing is null;
            switch (fact)
            {
                case PolicyCampaignLaunched value:
                    var kind = value.Subject?.Kind switch
                    {
                        "policy" => nameof(LaunchPolicyCampaign),
                        "training" => nameof(LaunchTrainingCampaign),
                        _ => null,
                    };
                    if (launch is not null || revision != 0 || value.TenantId != owner.TenantId || value.CampaignId != owner.Id ||
                        value.ProgramId == Uuid.Empty || value.Subject is not { RecordId: var material, Version: > 0 } ||
                        material == Uuid.Empty || kind is null || value.Operation.LogicalId != owner.Id ||
                        !Root(owner.Stream, value.Operation, kind, null, owner.Stream, value.Actor, value.LaunchedAt) ||
                        !Member(value.Actor, value.ActorMemberId) || value.RosterSnapshotId == Uuid.Empty ||
                        !snapshots.Add(value.RosterSnapshotId) || !Reference(value.AudienceTeamsManifest, PolicyCampaignContent.TeamsSchema) ||
                        !Reference(value.FirstResponse, FirstResponses.Schema) || !MutationResult(value.OperationResult))
                        throw Invalid("The campaign must begin with its exact native launch and actual launching member.");
                    launch = value;
                    latestRoster = value.RosterSnapshotId;
                    revision = 1;
                    break;
                case PolicyCampaignAudienceFrozen value:
                    if (launch is null || frozen is not null || currentIndex != 1 || value.TenantId != owner.TenantId ||
                        value.CampaignId != owner.Id || value.Batch != 1 ||
                        value.Operation != (launch.Operation with { LogicalId = AggregateIdentity.Create("PolicyCampaignAudienceFrozen", owner.Id) }) ||
                        value.FirstResponse is not null || value.OperationResult is not null ||
                        !Reference(value.EntriesManifest, PolicyCampaignContent.EntriesSchema))
                        throw Invalid("The complete audience is frozen as the exact atomic launch follow-up.");
                    frozen = value;
                    break;
                case PolicyCampaignAudienceAmended value:
                    if (!open || value.TenantId != owner.TenantId || value.CampaignId != owner.Id ||
                        value.Reconciliation != reconciliation + 1 || value.Batch != 1 ||
                        value.RosterSnapshotId == Uuid.Empty || !snapshots.Add(value.RosterSnapshotId) ||
                        !Root(owner.Stream, value.Operation, nameof(ReconcileCampaignAudience), (ulong)revision,
                            owner.Stream, value.Actor, value.AmendedAt) ||
                        !Reference(value.FirstResponse, FirstResponses.Schema) || !MutationResult(value.OperationResult) ||
                        !Reference(value.AmendmentsManifest, PolicyCampaignContent.AmendmentsSchema))
                        throw Invalid("The reconciliation must follow the exact open campaign without reusing a roster snapshot.");
                    reconciliation++;
                    revision++;
                    latestRoster = value.RosterSnapshotId;
                    break;
                case PolicyParticipantResultAdmitted value:
                    if (!open || value.Owner != owner.Stream || value.Revision != revision + 1 || value.Generation != generation ||
                        value.Operation.CommandKind is not (nameof(AcknowledgePolicy) or nameof(RecordTrainingCompletion) or nameof(ApproveCampaignWaiver)) ||
                        !ParticipantTarget(owner, value.Operation.AffectedOwner) ||
                        !Root(owner.Stream, value.Operation, value.Operation.CommandKind, (ulong)revision,
                            value.Operation.AffectedOwner, value.Operation.Actor, value.Operation.AcceptedAt) ||
                        !Reference(value.Intent, ChildAdmissionIntent.Schema) || !Reference(value.FirstResponse, FirstResponses.Schema))
                        throw Invalid("A participant result must be admitted on its exact open campaign generation and original revision.");
                    revision++;
                    admissions++;
                    break;
                case PolicyCampaignClosureAccepted value:
                    if (!open || value.Owner != owner.Stream || value.Revision != revision + 1 || value.Generation != generation ||
                        !Root(owner.Stream, value.Operation, nameof(CloseCampaign), (ulong)revision,
                            owner.Stream, value.Operation.Actor, value.Operation.AcceptedAt) ||
                        !Reference(value.Intent, ClosureIntent.Schema) || !Reference(value.FirstResponse, FirstResponses.Schema) ||
                        !closures.TryAdd(value.Operation.RootOperationId, value))
                        throw Invalid("A closure must seal its exact open generation once at the original campaign revision.");
                    pending = value;
                    revision++;
                    break;
                case PolicyCampaignClosureCanceled value:
                    if (pending is null || value.Owner != owner.Stream || value.Revision != revision + 1 ||
                        value.Generation != pending.Generation || value.Intent != pending.Intent ||
                        !ClosureChild(owner, value.Operation, pending, nameof(AbortPolicyCampaignClosure)) ||
                        !canceled.TryAdd(value.Operation.RootOperationId, value))
                        throw Invalid("Only the exact pending closure can cancel and reopen its next generation.");
                    pending = null;
                    generation++;
                    revision++;
                    break;
                case PolicyCampaignClosed value:
                    if (pending is null || value.TenantId != owner.TenantId || value.CampaignId != owner.Id ||
                        value.Intent != pending.Intent || value.AudienceGeneration != reconciliation ||
                        !ClosureChild(owner, value.Operation, pending, nameof(CompletePolicyCampaignClosure)) ||
                        value.Actor != pending.Operation.Actor || value.ClosedAt != pending.Operation.AcceptedAt ||
                        !Reference(value.TotalsContent, PolicyCampaignContent.TotalsSchema) ||
                        !Reference(value.ParticipantProofs, PolicyCampaignContent.ParticipantProofsSchema) ||
                        !MutationResult(value.OperationResult))
                        throw Invalid("Only the exact pending closure can complete with its frozen original actor, time and proof references.");
                    closing = value;
                    pending = null;
                    revision++;
                    break;
                default:
                    throw Invalid("The campaign history contains another native fact family.");
            }
        }
        if (!Same(owner.Launch, launch) || !Same(owner.Frozen, frozen) || !Same(owner.PendingClosure, pending) ||
            !Same(owner.Closing, closing) || owner.Revision != revision || owner.Reconciliations != reconciliation ||
            owner.Generation != generation || owner.AdmissionCount != admissions || owner.LatestRosterSnapshotId != latestRoster)
            throw Invalid("The full campaign native history differs from its hydrated bounded current authority.");
        owner.SelectedRosterSnapshotSeen = snapshots.Contains(owner.SelectedRosterSnapshotId);
        owner.SelectedClosure = closures.GetValueOrDefault(owner.SelectedClosureRootId);
        owner.SelectedCancellation = canceled.GetValueOrDefault(owner.SelectedClosureRootId);
    }

    static void Participant(PolicyCampaignParticipant owner, IReadOnlyList<DomainEvent> facts)
    {
        var roots = new HashSet<Uuid>();
        var acknowledgement = false;
        var completion = false;
        var revision = 0L;
        DomainEvent? selected = null;
        var uses = new SourceUseHistoryValidation(owner.Stream);
        foreach (var fact in facts)
        {
            uses.ObserveOwnerFact(fact);
            if (SourceUseFactKinds.Classify(fact) is { } use)
                uses.Observe(use.Purpose, use.Granted, (ISourceUseFact)fact);
            switch (fact)
            {
                case PolicyAcknowledgementRecorded value:
                    if (acknowledgement || !Scope(owner, value.TenantId, value.CampaignId, value.PersonId) ||
                        value.AcknowledgementId != value.Operation.RootOperationId || !roots.Add(value.Operation.RootOperationId) ||
                        !ResultChild(owner, value.Operation, value.Recorder, value.AcknowledgedAt) ||
                        value.PolicyId == Uuid.Empty || value.PolicyVersion < 1 || !ValidActor(value.Performer) ||
                        !Reference(value.Intent, ChildAdmissionIntent.Schema) || !MutationResult(value.OperationResult))
                        throw Invalid("A participant records one exact admitted acknowledgement with its original attribution.");
                    acknowledgement = true;
                    revision++;
                    if (value.Operation.RootOperationId == owner.SelectedResultRootId)
                        selected = value;
                    break;
                case TrainingCompletionRecorded value:
                    if (completion || !Scope(owner, value.TenantId, value.CampaignId, value.PersonId) ||
                        value.CompletionId != value.Operation.RootOperationId || !roots.Add(value.Operation.RootOperationId) ||
                        !ResultChild(owner, value.Operation, value.Recorder, value.RecordedAt) ||
                        value.RequirementId == Uuid.Empty || value.RequirementVersion < 1 ||
                        !Reference(value.Intent, ChildAdmissionIntent.Schema) || !MutationResult(value.OperationResult))
                        throw Invalid("A participant records one exact admitted training completion with its original attribution.");
                    completion = true;
                    revision++;
                    if (value.Operation.RootOperationId == owner.SelectedResultRootId)
                        selected = value;
                    break;
                case CampaignWaiverApproved value:
                    if (!Scope(owner, value.TenantId, value.CampaignId, value.PersonId) ||
                        value.WaiverId != value.Operation.RootOperationId || !roots.Add(value.Operation.RootOperationId) ||
                        !ResultChild(owner, value.Operation, value.Approver, value.ApprovedAt) ||
                        !Member(value.Approver, value.ApproverMemberId) ||
                        !Reference(value.Intent, ChildAdmissionIntent.Schema) || !MutationResult(value.OperationResult))
                        throw Invalid("An exception must retain one unique admitted root and the actual original approving member.");
                    revision++;
                    if (value.Operation.RootOperationId == owner.SelectedResultRootId)
                        selected = value;
                    break;
                case PolicyCampaignParticipantCampaignClosureUseAuthorized value:
                    if (value.Owner != owner.Stream || value.Revision != revision ||
                        !Reference(value.Intent, SourceUseAuthorizationIntent.Schema))
                        throw Invalid("A campaign-closure use must hold the actual applied participant revision.");
                    break;
                case PolicyCampaignParticipantCampaignClosureUseSettled value:
                    if (value.Owner != owner.Stream || value.Revision != revision ||
                        !Reference(value.Intent, SourceUseAuthorizationIntent.Schema))
                        throw Invalid("A campaign-closure settlement must retain its actual owner and current applied revision.");
                    break;
                default:
                    throw Invalid("The participant history contains another native fact family.");
            }
        }
        if (owner.Revision != revision || owner.HasAcknowledgement != acknowledgement || owner.HasCompletion != completion ||
            !Same(owner.SelectedResult, selected))
            throw Invalid("The full participant native history differs from its hydrated bounded current authority.");
        owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        owner.SelectedResult = selected;
    }

    static bool Scope(PolicyCampaignParticipant owner, Uuid tenant, Uuid campaign, Uuid person) =>
        tenant == owner.TenantId && campaign == owner.CampaignId && person == owner.PersonId;
    static bool ResultChild(PolicyCampaignParticipant owner, OperationProvenance operation, ActorReference actor,
        DateTimeOffset originalAt) => operation.CommandKind == nameof(ApplyPolicyParticipantResult) &&
        operation.Owner == owner.Stream && operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream &&
        (operation.FirstOwner ?? operation.Owner) == new PolicyDistributionCampaign(owner.TenantId, owner.CampaignId).Stream &&
        operation.CausationEventId is { } cause && cause != Uuid.Empty && operation.Actor == actor && ValidActor(actor) && originalAt != default;
    static bool ClosureChild(PolicyDistributionCampaign owner, OperationProvenance operation,
        PolicyCampaignClosureAccepted accepted, string kind) => operation.CommandKind == kind && operation.Owner == owner.Stream &&
        operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream &&
        (operation.FirstOwner ?? operation.Owner) == owner.Stream && operation.RootOperationId == accepted.Operation.RootOperationId &&
        operation.CausationEventId == accepted.Metadata.EventId && operation.OriginalExpectedRevision == accepted.Operation.OriginalExpectedRevision &&
        operation.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256 && operation.Actor == accepted.Operation.Actor &&
        operation.Episode == accepted.Operation.Episode;
    static bool ParticipantTarget(PolicyDistributionCampaign owner, EventStreamAddress? target) => target is not null &&
        target.Realm == owner.Stream.Realm && target.Area == PolicyCampaignParticipant.Area &&
        Uuid.TryParse(target.Resource, CultureInfo.InvariantCulture, out var identity) && identity != Uuid.Empty;
    static bool Root(EventStreamAddress owner, OperationProvenance operation, string kind, ulong? expected,
        EventStreamAddress affected, ActorReference actor, DateTimeOffset at) => operation.LogicalId == operation.RootOperationId &&
        operation.CausationEventId is null && operation.CommandKind == kind && operation.Owner == owner &&
        (operation.FirstOwner ?? operation.Owner) == owner && operation.AffectedOwner == affected && operation.ExpectedRevisionOwner == owner &&
        operation.OriginalExpectedRevision == expected && operation.Episode == PolicyCampaignContent.InitialEpisode &&
        operation.Actor == actor && ValidActor(actor) && operation.AcceptedAt == at;
    internal static bool Reference(ImmutableContentRef? value, string schema) => value is
    { ContentId: var id, Sha256: { Length: 64 } digest, Bytes: > 0, MediaType: "application/json" } &&
        id != Uuid.Empty && digest.All(Uri.IsHexDigit) && value.Schema == schema;
    static bool MutationResult(ImmutableContentRef? value) => Reference(value, "mutation-result-content.v1");
    static bool ValidActor(ActorReference? actor) => actor is not null && !string.IsNullOrWhiteSpace(actor.Kind) &&
        !string.IsNullOrWhiteSpace(actor.Id) && !string.IsNullOrWhiteSpace(actor.Display);
    static bool Member(ActorReference? actor, Uuid member) => member != Uuid.Empty && actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var actual) && actual == member;
    static bool Same(DomainEvent? left, DomainEvent? right) => left is null ? right is null : right is not null &&
        left.GetType() == right.GetType() && left.Equals(right) && left.Metadata == right.Metadata;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
