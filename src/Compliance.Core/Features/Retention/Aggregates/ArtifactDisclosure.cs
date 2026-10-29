using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Retention.Aggregates;

/// <summary>One explicitly approved, delivered, and revocable disclosure record.</summary>
public sealed class ArtifactDisclosure : Aggregate
{
    const int MaximumTextLength = 2000;
    const int MaximumRecipientLength = 500;
    static readonly HashSet<string> HandlingClasses = new(StringComparer.Ordinal)
    {
        "public", "internal", "confidential", "restricted"
    };

    readonly Uuid _tenantId;
    ArtifactDisclosureView? _current;

    public long Revision => _current?.Revision ?? 0;

    public ArtifactDisclosureView? Current => _current;

    public ArtifactDisclosure(Uuid tenantId, Uuid disclosureId)
        : base(disclosureId, new EventStreamAddress(tenantId.ToString(), "artifact-disclosures",
            disclosureId.ToString()))
    {
        _tenantId = tenantId;
        On<ArtifactDisclosureApproved>(Apply);
        On<ArtifactDisclosureDelivered>(Apply);
        On<ArtifactDisclosureRevoked>(Apply);
    }

    public Result Approve(ArtifactDisclosureSourceSnapshot source, string recipient,
        string purpose, string rationale, ActorReference approvedBy, DateTimeOffset approvedAt)
    {
        if (ValidateSource(source, requireAvailable: true) is { } sourceError)
            return Result.Failure(sourceError);
        if (!ValidActor(approvedBy) || approvedAt == default ||
            !Bounded(recipient, 1, MaximumRecipientLength) ||
            !Bounded(purpose, 1, MaximumTextLength) || !Bounded(rationale, 1, MaximumTextLength))
            return Invalid("Disclosure approval requires a recipient, purpose, rationale, and personal approver.");
        recipient = recipient.Trim();
        purpose = purpose.Trim();
        rationale = rationale.Trim();
        if (_current is { } existing)
            return existing.Status == "approved" && SameSource(existing, source) &&
                   existing.Recipient == recipient && existing.Purpose == purpose &&
                   existing.Rationale == rationale && existing.ApprovedBy == approvedBy &&
                   existing.ApprovedAt == approvedAt
                ? Result.Success
                : Conflict("A disclosure identity cannot be reused with changed approval intent.");

        var view = new ArtifactDisclosureView(_tenantId, Id, source.Source.SourceId,
            source.Source with { }, source.ContentLength, source.HandlingClass,
            source.HandlingPolicyRevision, source.Position, recipient, purpose, rationale,
            approvedBy, approvedAt, "approved", 1, null, null, null, null, false);
        RaiseEvent(new ArtifactDisclosureApproved(_tenantId, Id, 1, MemberId(approvedBy), view,
            approvedAt));
        return Result.Success;
    }

    public Result Deliver(ArtifactDisclosureSourceSnapshot source, long expectedRevision,
        string deliveryReference, ActorReference deliveredBy, DateTimeOffset deliveredAt)
    {
        if (_current is not { } current)
            return Missing();
        if (!ValidActor(deliveredBy) || deliveredAt == default ||
            !Bounded(deliveryReference, 1, MaximumRecipientLength))
            return Invalid("Disclosure delivery requires a bounded receipt reference and personal actor.");
        deliveryReference = deliveryReference.Trim();
        if (current.Delivery is { } existingDelivery)
            return SameSource(current, source) && existingDelivery.DeliveryReference == deliveryReference &&
                   existingDelivery.DeliveredBy == deliveredBy && existingDelivery.DeliveredAt == deliveredAt
                ? Result.Success
                : Conflict("A disclosure delivery receipt is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "approved")
            return Conflict("Only an approved disclosure can be delivered.");
        if (ValidateSource(source, requireAvailable: true) is { } sourceError)
            return Result.Failure(sourceError);
        if (!SameSource(current, source))
            return Conflict("The artifact content or handling policy changed after disclosure approval.");

        var delivered = current with
        {
            Status = "delivered",
            Revision = Revision + 1,
            Delivery = new ArtifactDisclosureDeliveryView(deliveryReference, deliveredBy,
                deliveredAt, source.Position),
            DownloadedCopiesRecallable = false,
        };
        RaiseEvent(new ArtifactDisclosureDelivered(_tenantId, Id, delivered.Revision,
            MemberId(deliveredBy), delivered, deliveredAt));
        return Result.Success;
    }

    public Result Revoke(long expectedRevision, string rationale, ActorReference revokedBy,
        DateTimeOffset revokedAt)
    {
        if (_current is not { } current)
            return Missing();
        if (!ValidActor(revokedBy) || revokedAt == default ||
            !Bounded(rationale, 1, MaximumTextLength))
            return Invalid("Disclosure revocation requires a bounded rationale and personal actor.");
        rationale = rationale.Trim();
        if (current.Status == "revoked")
            return current.RevocationRationale == rationale && current.RevokedBy == revokedBy &&
                   current.RevokedAt == revokedAt && expectedRevision <= Revision
                ? Result.Success
                : Conflict("A disclosure revocation decision is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status is not ("approved" or "delivered"))
            return Conflict("Only an approved disclosure can be revoked.");

        var revoked = current with
        {
            Status = "revoked",
            Revision = Revision + 1,
            RevokedBy = revokedBy,
            RevocationRationale = rationale,
            RevokedAt = revokedAt,
            DownloadedCopiesRecallable = false,
        };
        RaiseEvent(new ArtifactDisclosureRevoked(_tenantId, Id, revoked.Revision,
            MemberId(revokedBy), revoked, revokedAt));
        return Result.Success;
    }

    void Apply(ArtifactDisclosureApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "approved");

    void Apply(ArtifactDisclosureDelivered ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "delivered");

    void Apply(ArtifactDisclosureRevoked ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "revoked");

    void ApplyState(Uuid tenantId, Uuid disclosureId, long revision, Uuid? actorMemberId,
        ArtifactDisclosureView state, DateTimeOffset occurredAt, string transition)
    {
        if (tenantId != _tenantId || disclosureId != Id || state is null ||
            state.TenantId != _tenantId || state.DisclosureId != Id ||
            state.Revision != revision || state.Status != transition ||
            state.ArtifactId != state.Source.SourceId || state.Source.TenantId != _tenantId ||
            actorMemberId != TransitionActor(state, transition) ||
            ValidateState(state) is not null ||
            transition == "approved" && (_current is not null || revision != 1 || state.ApprovedAt != occurredAt) ||
            transition != "approved" && (_current is null || revision != Revision + 1 ||
                !SameApproval(_current, state) ||
                transition == "delivered" && (state.Delivery is null || state.Delivery.DeliveredAt != occurredAt ||
                    _current.Status != "approved") ||
                transition == "revoked" && (state.RevokedAt != occurredAt ||
                    _current.Status is not ("approved" or "delivered"))))
            throw new InvalidOperationException("The artifact disclosure event is invalid.");
        _current = FreezeView(state);
    }

    RequestError? ValidateSource(ArtifactDisclosureSourceSnapshot source, bool requireAvailable)
    {
        if (source is null || source.Source is null || source.Source.TenantId != _tenantId ||
            source.Source.SourceKind != "evidence_artifact" || source.Source.SourceId == Uuid.Empty)
            return new(RequestErrorKind.NotFound, "The evidence artifact was not found in this tenant.");
        if (!ValidDigest(source.Source.ContentSha256) || source.Position == 0 ||
            source.ContentLength <= 0 || !HandlingClasses.Contains(source.HandlingClass) ||
            source.HandlingPolicyRevision <= 0 || source.ArtifactState is not ("available" or "quarantined" or
                "pending_inspection" or "rejected" or "disposed"))
            return new(RequestErrorKind.Conflict, "The evidence identity or handling policy is not current.");
        if (requireAvailable && source.ArtifactState != "available")
            return new(RequestErrorKind.Conflict,
                "Only available, inspected evidence can be disclosed.");
        return null;
    }

    static RequestError? ValidateState(ArtifactDisclosureView state)
    {
        if (state.Source is null || state.Source.SourceKind != "evidence_artifact" ||
            state.Source.ContentSha256 is not { Length: 64 } digest || !ValidDigest(digest) ||
            state.ContentLength <= 0 || !HandlingClasses.Contains(state.HandlingClass) ||
            state.HandlingPolicyRevision <= 0 || state.ApprovedSourcePosition == 0 ||
            !Bounded(state.Recipient, 1, MaximumRecipientLength) ||
            !Bounded(state.Purpose, 1, MaximumTextLength) ||
            !Bounded(state.Rationale, 1, MaximumTextLength) || !ValidActor(state.ApprovedBy) ||
            state.ApprovedAt == default || state.Revision is < 1 or > 3 ||
            state.Status is not ("approved" or "delivered" or "revoked") ||
            state.Status == "delivered" && state.Delivery is null ||
            state.Status == "revoked" && state.Revision == 3 && state.Delivery is null ||
            state.Status == "approved" && state.Revision != 1 ||
            state.Status == "delivered" && state.Revision != 2 ||
            state.Status == "revoked" && state.Revision is not (2 or 3) ||
            state.Delivery is { } delivery && (string.IsNullOrWhiteSpace(delivery.DeliveryReference) ||
                delivery.DeliveryReference.Length > MaximumRecipientLength || !ValidActor(delivery.DeliveredBy) ||
                delivery.DeliveredAt < state.ApprovedAt || delivery.SourcePosition < state.ApprovedSourcePosition) ||
            state.Status == "revoked" && (!ValidActor(state.RevokedBy) ||
                !Bounded(state.RevocationRationale, 1, MaximumTextLength) ||
                state.RevokedAt is null || state.RevokedAt < state.ApprovedAt) ||
            state.Status != "revoked" && (state.RevokedBy is not null ||
                state.RevocationRationale is not null || state.RevokedAt is not null) ||
            state.DownloadedCopiesRecallable)
            return new(RequestErrorKind.Validation, "The retained artifact disclosure state is invalid.");
        return null;
    }

    static bool SameSource(ArtifactDisclosureView view, ArtifactDisclosureSourceSnapshot source) =>
        view.Source == source.Source && view.ContentLength == source.ContentLength &&
        view.HandlingClass == source.HandlingClass &&
        view.HandlingPolicyRevision == source.HandlingPolicyRevision &&
        source.Position >= view.ApprovedSourcePosition;

    static bool SameApproval(ArtifactDisclosureView first, ArtifactDisclosureView second) =>
        first.TenantId == second.TenantId && first.DisclosureId == second.DisclosureId &&
        first.ArtifactId == second.ArtifactId && first.Source == second.Source &&
        first.ContentLength == second.ContentLength && first.HandlingClass == second.HandlingClass &&
        first.HandlingPolicyRevision == second.HandlingPolicyRevision &&
        first.ApprovedSourcePosition == second.ApprovedSourcePosition &&
        first.Recipient == second.Recipient && first.Purpose == second.Purpose &&
        first.Rationale == second.Rationale && first.ApprovedBy == second.ApprovedBy &&
        first.ApprovedAt == second.ApprovedAt &&
        first.DownloadedCopiesRecallable == second.DownloadedCopiesRecallable;

    static ArtifactDisclosureView FreezeView(ArtifactDisclosureView state) => state with
    {
        Source = state.Source with { },
    };

    static Uuid? TransitionActor(ArtifactDisclosureView state, string transition) => transition switch
    {
        "approved" => MemberId(state.ApprovedBy),
        "delivered" => state.Delivery is null ? null : MemberId(state.Delivery.DeliveredBy),
        "revoked" => state.RevokedBy is null ? null : MemberId(state.RevokedBy),
        _ => null,
    };

    static Uuid? MemberId(ActorReference actor) => Uuid.TryParse(actor.Id, out var id) ? id : null;

    static bool ValidActor(ActorReference? actor) => actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var memberId) && memberId != Uuid.Empty;

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The artifact disclosure was not found."));

    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    Result StaleRevision() => Conflict($"The disclosure revision is {Revision}.");
}
