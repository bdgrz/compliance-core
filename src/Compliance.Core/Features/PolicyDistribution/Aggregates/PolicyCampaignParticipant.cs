using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;

/// <summary>
///     One person's results in one campaign, identified by <c>(campaignId, personId)</c>. It owns the exact
///     acknowledgement, completion and exception facts the campaign admitted for that included person, and embeds
///     <see cref="SourceUseReservations" />: a held campaign-closure use does not advance the logical revision. The
///     logical revision counts the applied results only. Every applied result carries the root operation id of the
///     admission it applies, so a replayed receiver can never apply an admitted result twice.
/// </summary>
public sealed class PolicyCampaignParticipant : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every participant (the realm is the tenant).</summary>
    public const string Area = "policy-campaign-participants";

    readonly PolicyCampaignReplayCut _cut = new();
    long _revision;
    bool _hasAcknowledgement;
    bool _hasCompletion;

    /// <summary>Opens the participant of one person in one campaign, selecting the logical operation to resolve.</summary>
    public PolicyCampaignParticipant(Uuid tenantId, Uuid campaignId, Uuid personId, Uuid selectedLogicalId = default, Uuid selectedResultRootId = default)
        : base(IdentityOf(campaignId, personId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(campaignId, personId).ToString()))
    {
        (TenantId, CampaignId, PersonId) = (tenantId, campaignId, personId);
        SelectedResultRootId = selectedResultRootId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<PolicyAcknowledgementRecorded>(ev =>
        {
            if (ev.Operation is null)
                throw new ImmutableContentIntegrityException("The native participant fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native participant operation occurs more than once.");
            Acceptance.Observe(ev);
            if (_hasAcknowledgement || !Owns(ev.TenantId, ev.CampaignId, ev.PersonId, ev.Operation))
                throw new ImmutableContentIntegrityException("A participant records one acknowledgement of its own campaign and person.");
            _hasAcknowledgement = true;
            Add(ev, ev.Operation);
        });
        On<TrainingCompletionRecorded>(ev =>
        {
            if (ev.Operation is null)
                throw new ImmutableContentIntegrityException("The native participant fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native participant operation occurs more than once.");
            Acceptance.Observe(ev);
            if (_hasCompletion || !Owns(ev.TenantId, ev.CampaignId, ev.PersonId, ev.Operation))
                throw new ImmutableContentIntegrityException("A participant records one completion of its own campaign and person.");
            _hasCompletion = true;
            Add(ev, ev.Operation);
        });
        On<CampaignWaiverApproved>(ev =>
        {
            if (ev.Operation is null)
                throw new ImmutableContentIntegrityException("The native participant fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native participant operation occurs more than once.");
            Acceptance.Observe(ev);
            if (!Owns(ev.TenantId, ev.CampaignId, ev.PersonId, ev.Operation))
                throw new ImmutableContentIntegrityException("A participant records exceptions of its own campaign and person.");
            Add(ev, ev.Operation);
        });
        On<PolicyCampaignParticipantCampaignClosureUseAuthorized>(granted =>
        {
            if (granted.Operation is null)
                throw new ImmutableContentIntegrityException("The native participant fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == granted.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native participant operation occurs more than once.");
            Acceptance.Observe(granted);
            if (granted.Revision != _revision)
                throw new ImmutableContentIntegrityException("A closure use is held at the participant's applied revision.");
            Uses.ApplyGranted(SourceUsePurpose.CampaignClosure, granted);
        });
        On<PolicyCampaignParticipantCampaignClosureUseSettled>(settled =>
        {
            if (settled.Operation is null)
                throw new ImmutableContentIntegrityException("The native participant fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == settled.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native participant operation occurs more than once.");
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.CampaignClosure, settled);
        });
    }

    /// <summary>The tenant of the campaign.</summary>
    public Uuid TenantId { get; }

    /// <summary>The campaign this participant belongs to.</summary>
    public Uuid CampaignId { get; }

    /// <summary>The included person.</summary>
    public Uuid PersonId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The held campaign-closure uses and their fences.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>The logical revision: the number of results applied to this participant.</summary>
    public long Revision => _revision;

    internal PolicyCampaignReplayCut ReplayCut => _cut;
    internal Uuid SelectedResultRootId { get; }
    internal DomainEvent? SelectedResult { get; set; }
    public bool HasAcknowledgement => _hasAcknowledgement;
    public bool HasCompletion => _hasCompletion;

    /// <summary>The identity of the participant of a campaign and person.</summary>
    public static Uuid IdentityOf(Uuid campaignId, Uuid personId) =>
        AggregateIdentity.Create(nameof(PolicyCampaignParticipant), campaignId, personId);

    /// <summary>The stream of the participant of a campaign and person.</summary>
    public static EventStreamAddress StreamOf(Uuid tenantId, Uuid campaignId, Uuid personId) =>
        new(tenantId.ToString(), Area, IdentityOf(campaignId, personId).ToString());

    /// <summary>Applies an admitted acknowledgement as a receiver fact; the caller proved the admission.</summary>
    public Result RecordAcknowledgement(OperationProvenance operation, Uuid policyId, long policyVersion,
        string contentSha256, string acknowledgementText, ActorReference performer, ActorReference recorder,
        bool recordedOnBehalf, DateTimeOffset acknowledgedAt, ImmutableContentRef intent,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareApply(operation);
        if (!prepared.IsSuccess)
            return prepared;
        if (_hasAcknowledgement)
            return Conflict("The person already acknowledged this campaign.");
        RaiseEvent(new PolicyAcknowledgementRecorded(TenantId, CampaignId, operation.RootOperationId, PersonId,
            policyId, policyVersion, contentSha256, acknowledgementText, performer, recorder, recordedOnBehalf,
            acknowledgedAt, operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Applies an admitted completion as a receiver fact; the caller proved the admission.</summary>
    public Result RecordCompletion(OperationProvenance operation, Uuid requirementId, long requirementVersion,
        DateOnly completedOn, string source, string evidenceReference, ActorReference recorder,
        DateTimeOffset recordedAt, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareApply(operation);
        if (!prepared.IsSuccess)
            return prepared;
        if (_hasCompletion)
            return Conflict("A completion is already recorded for this person.");
        RaiseEvent(new TrainingCompletionRecorded(TenantId, CampaignId, operation.RootOperationId, PersonId,
            requirementId, requirementVersion, completedOn, source, evidenceReference, recorder, recordedAt,
            operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Applies an admitted exception as a receiver fact; the caller proved the admission.</summary>
    public Result ApproveWaiver(OperationProvenance operation, string reason, DateOnly expiresOn,
        ActorReference approver, Uuid approverMemberId, DateTimeOffset approvedAt, ImmutableContentRef intent,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareApply(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new CampaignWaiverApproved(TenantId, CampaignId, operation.RootOperationId, PersonId, reason,
            expiresOn, approver, approverMemberId, approvedAt, operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Holds the participant's applied results for the campaign closure.</summary>
    public Result AuthorizeClosureUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A closure use is held on the participant's own stream."));
        Uses.RequireForAppend(SourceUsePurpose.CampaignClosure, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new PolicyCampaignParticipantCampaignClosureUseAuthorized(Stream, Revision, generation, intent,
            null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a closure use, or fences its key when it was never granted.</summary>
    public Result SettleClosureUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held participant stream."));
        Uses.RequireForAppend(SourceUsePurpose.CampaignClosure, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new PolicyCampaignParticipantCampaignClosureUseSettled(Stream, Revision, generation, intent,
            null, operation, null, null));
        return Result.Success;
    }

    Result PrepareApply(OperationProvenance operation)
    {
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An applied result is recorded on the participant's own stream."));
        if (SelectedResultRootId == operation.RootOperationId && SelectedResult is not null)
            return Conflict("The admitted result was already applied to this participant.");
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The participant is held by a campaign closure; its results can no longer change.", true));
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        return SelectedResultRootId == operation.RootOperationId || Version == 0 && CommittedStreamPosition == 0
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A participant application requires its exact selected admitted root at the finalized native cut.", true));
    }

    bool Owns(Uuid tenantId, Uuid campaignId, Uuid personId, OperationProvenance operation) =>
        tenantId == TenantId && campaignId == CampaignId && personId == PersonId && operation.Owner == Stream;

    void Add(DomainEvent fact, OperationProvenance operation)
    {
        _revision++;
        if (operation.RootOperationId == SelectedResultRootId)
            SelectedResult = fact;
    }

    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
    protected override void Apply(DomainEvent ev)
    {
        _cut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
