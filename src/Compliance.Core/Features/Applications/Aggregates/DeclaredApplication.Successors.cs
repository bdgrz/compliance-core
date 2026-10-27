using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class DeclaredApplication
{
    public IReadOnlyList<ApplicationRelationshipView> Relationships =>
        Array.AsReadOnly(_relationships.Values
            .Where(static relationship => relationship.IsActive)
            .Select(static relationship => relationship.View)
            .OrderBy(static relationship => relationship.RelationshipId.ToString(),
                StringComparer.Ordinal).ToArray());

    public ApplicationRelationshipView? Relationship(string relationshipType,
        Uuid targetApplicationId) => _relationships.GetValueOrDefault(
        ApplicationRelationshipIdentity.Id(Id, relationshipType, targetApplicationId))?.View;

    public Result<ApplicationRelationshipRegistration> RecordRelationship(
        Uuid targetApplicationId, long sourceApplicationRevision,
        long targetApplicationRevision, string relationshipType, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || _retired)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict, "Relationships require an active source application."));
        if (targetApplicationId == Uuid.Empty || targetApplicationId == Id ||
            sourceApplicationRevision != _revision || targetApplicationRevision < 1 ||
            !ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            changedAt == default)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application relationship is invalid."));

        var relationshipId = ApplicationRelationshipIdentity.Id(Id, relationshipType,
            targetApplicationId);
        var current = _relationships.GetValueOrDefault(relationshipId);
        if (current is { IsActive: true })
        {
            if (current.View.SourceApplicationRevision != sourceApplicationRevision ||
                current.View.TargetApplicationRevision != targetApplicationRevision)
                return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The existing relationship was recorded against different application revisions."));
            return Result<ApplicationRelationshipRegistration>.Success(
                new ApplicationRelationshipRegistration(relationshipId,
                    current.View.Revision, current.View.Status));
        }
        if (_relationships.Values.Any(relationship => relationship.IsActive &&
            relationship.View.TargetApplicationId == targetApplicationId &&
            relationship.View.RelationshipType == relationshipType))
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "An active relationship of this type already exists for the target."));

        var revision = (current?.View.Revision ?? 0) + 1;
        var status = relationshipType == ApplicationRelationshipIdentity.Replaces
            ? "proposed"
            : "active";
        RaiseEvent(new ApplicationRelationshipRecorded(_tenantId, relationshipId, Id,
            targetApplicationId, relationshipType, revision, status, _revision,
            targetApplicationRevision, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result<ApplicationRelationshipRegistration>.Success(
            new ApplicationRelationshipRegistration(relationshipId, revision, status));
    }

    public CommandFailure? RemoveRelationship(Uuid targetApplicationId,
        long expectedRelationshipRevision, string relationshipType, string reason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The source application was not found.");
        if (!ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            targetApplicationId == Uuid.Empty || targetApplicationId == Id ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 2000 ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            changedAt == default || expectedRelationshipRevision < 1)
            return CommandFailure.InvalidContent(
                "The application relationship removal is invalid.");

        var relationshipId = ApplicationRelationshipIdentity.Id(Id, relationshipType,
            targetApplicationId);
        var current = _relationships.GetValueOrDefault(relationshipId);
        if (current is null || !current.IsActive)
            return null;
        if (expectedRelationshipRevision != current.View.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "application relationship", current.View.Revision));

        RaiseEvent(new ApplicationRelationshipGovernedRemoved(_tenantId, relationshipId,
            Id, targetApplicationId, relationshipType, current.View.Revision + 1,
            reason.Trim(), actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? ApproveSuccessor(Uuid targetApplicationId,
        long expectedSourceRevision, long expectedSourceRelationshipRevision,
        long expectedRelationshipRevision,
        long expectedTargetRevision, string impactDigest, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset approvedAt)
    {
        if (!_created || _retired)
            return CommandFailure.MissingRecord("The predecessor application was not found.");
        if (expectedSourceRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "application", _revision));
        if (expectedSourceRelationshipRevision != _relationshipRevision)
            return CommandFailure.StateConflict(
                "The application relationships changed. Reload before approval.");
        var relationship = Relationship(ApplicationRelationshipIdentity.Replaces,
            targetApplicationId);
        if (relationship is not { Status: "proposed" } ||
            relationship.Revision != expectedRelationshipRevision ||
            relationship.SourceApplicationRevision != expectedSourceRevision ||
            expectedTargetRevision < 1 ||
            relationship.TargetApplicationRevision != expectedTargetRevision ||
            !ApplicationRelationshipIdentity.IsImpactDigest(impactDigest) ||
            approverMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(approverDisplay) ||
            approvedAt == default)
            return CommandFailure.StateConflict(
                "The proposed successor or impact assessment changed. Reload before approval.");
        if (relationship.ChangedBy.Id == approverMemberId.ToString())
            return CommandFailure.StateConflict(
                "A successor must be approved by an active member other than its proposer.");

        RaiseEvent(new ApplicationRelationshipApproved(_tenantId,
            relationship.RelationshipId, Id, targetApplicationId,
            relationship.Revision + 1, _revision, expectedTargetRevision,
            impactDigest, approverMemberId, approverDisplay, approvedAt)
        {
            StoredApprover = ActorReference.ForMember(approverMemberId, approverDisplay),
        });
        return null;
    }

    void RegisterGovernedRelationshipEvents()
    {
        On<ApplicationRelationshipRecorded>(ApplyGovernedRelationshipRecorded);
        On<ApplicationRelationshipApproved>(ApplyGovernedRelationshipApproved);
        On<ApplicationRelationshipGovernedRemoved>(ApplyGovernedRelationshipRemoved);
    }

    void ApplyGovernedRelationshipRecorded(ApplicationRelationshipRecorded ev)
    {
        if (ev.TenantId != _tenantId || ev.SourceApplicationId != Id ||
            ev.RelationshipId != ApplicationRelationshipIdentity.Id(Id,
                ev.RelationshipType, ev.TargetApplicationId) || ev.Revision < 1 ||
            ev.SourceApplicationRevision != _revision || ev.TargetApplicationRevision < 1 ||
            !ApplicationRelationshipIdentity.IsSupported(ev.RelationshipType) ||
            ev.Status != (ev.RelationshipType == ApplicationRelationshipIdentity.Replaces
                ? "proposed" : "active") || ev.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ActorDisplay) || ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship event is invalid.");

        var current = _relationships.GetValueOrDefault(ev.RelationshipId);
        if (ev.Revision != (current?.View.Revision ?? 0) + 1 ||
            current is { IsActive: true } ||
            _relationships.Values.Any(relationship => relationship.IsActive &&
                relationship.View.TargetApplicationId == ev.TargetApplicationId &&
                relationship.View.RelationshipType == ev.RelationshipType))
            throw new InvalidOperationException("An application relationship event is out of order.");

        _relationshipRevision++;
        var view = new ApplicationRelationshipView(ev.RelationshipId,
            ev.TargetApplicationId, ev.RelationshipType, ev.ActorMemberId,
            ev.ActorDisplay, ev.ChangedAt)
        {
            TenantId = _tenantId,
            SourceApplicationId = Id,
            Revision = ev.Revision,
            Status = ev.Status,
            ChangedBy = ev.Actor,
            SourceApplicationRevision = ev.SourceApplicationRevision,
            TargetApplicationRevision = ev.TargetApplicationRevision,
        };
        _relationships[ev.RelationshipId] = new ApplicationRelationshipState(view, true,
            _relationshipRevision, (current?.EventCount ?? 0) + 1);
    }

    void ApplyGovernedRelationshipApproved(ApplicationRelationshipApproved ev)
    {
        var current = _relationships.GetValueOrDefault(ev.RelationshipId);
        if (ev.TenantId != _tenantId || ev.SourceApplicationId != Id ||
            current is not { IsActive: true, View.Status: "proposed" } ||
            current.View.Revision + 1 != ev.Revision ||
            current.View.TargetApplicationId != ev.TargetApplicationId ||
            ev.SourceApplicationRevision != _revision ||
            ev.SourceApplicationRevision != current.View.SourceApplicationRevision ||
            ev.TargetApplicationRevision != current.View.TargetApplicationRevision ||
            !ApplicationRelationshipIdentity.IsImpactDigest(ev.ImpactDigest) ||
            ev.ApproverMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ApproverDisplay) || ev.ApprovedAt == default ||
            ev.Approver != ActorReference.ForMember(ev.ApproverMemberId,
                ev.ApproverDisplay) ||
            current.View.ChangedBy.Id == ev.ApproverMemberId.ToString())
            throw new InvalidOperationException("An application successor approval is invalid.");

        _relationshipRevision++;
        _relationships[ev.RelationshipId] = current with
        {
            View = current.View with
            {
                Revision = ev.Revision,
                Status = "approved",
                ActorMemberId = ev.ApproverMemberId,
                ActorDisplay = ev.ApproverDisplay,
                ChangedBy = ev.Approver,
                ChangedAt = ev.ApprovedAt,
                Approval = new ApplicationRelationshipApproval(ev.ApproverMemberId,
                    ev.Approver, ev.SourceApplicationRevision,
                    ev.TargetApplicationRevision, ev.ImpactDigest, ev.ApprovedAt),
            },
            RelationshipRevision = _relationshipRevision,
            EventCount = current.EventCount + 1,
        };
    }

    void ApplyGovernedRelationshipRemoved(ApplicationRelationshipGovernedRemoved ev)
    {
        var current = _relationships.GetValueOrDefault(ev.RelationshipId);
        if (ev.TenantId != _tenantId || ev.SourceApplicationId != Id ||
            current is not { IsActive: true } || current.View.Revision + 1 != ev.Revision ||
            current.View.TargetApplicationId != ev.TargetApplicationId ||
            current.View.RelationshipType != ev.RelationshipType ||
            string.IsNullOrWhiteSpace(ev.Reason) || ev.Reason.Length > 2000 ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ActorDisplay) ||
            ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship removal is invalid.");

        _relationshipRevision++;
        _relationships[ev.RelationshipId] = current with
        {
            View = current.View with
            {
                Revision = ev.Revision,
                Status = "removed",
                ActorMemberId = ev.ActorMemberId,
                ActorDisplay = ev.ActorDisplay,
                ChangedBy = ev.Actor,
                ChangedAt = ev.ChangedAt,
                RemovalReason = ev.Reason,
                Approval = null,
            },
            IsActive = false,
            RelationshipRevision = _relationshipRevision,
            EventCount = current.EventCount + 1,
        };
    }
}
