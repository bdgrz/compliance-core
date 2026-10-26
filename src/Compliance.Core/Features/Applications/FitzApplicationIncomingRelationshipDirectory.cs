using Bdgrz.Compliance.Features.Applications.Events;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationIncomingRelationshipDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-incoming-relationships-v1/projection",
            ApplicationIncomingRelationshipProjector.ProjectorName),
        IApplicationIncomingRelationshipDirectory, IApplicationIncomingRelationshipProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ApplicationRelationshipAdded added:
                await ApplyLegacyAddedAsync(added, ct).ConfigureAwait(false);
                break;
            case ApplicationRelationshipRemoved removed:
                await ApplyLegacyRemovedAsync(removed, ct).ConfigureAwait(false);
                break;
            case ApplicationRelationshipRecorded recorded:
                await ApplyRecordedAsync(recorded, ct).ConfigureAwait(false);
                break;
            case ApplicationRelationshipGovernedRemoved removed:
                await ApplyRemovedAsync(removed, ct).ConfigureAwait(false);
                break;
            case ApplicationRelationshipApproved approved:
                await ApplyApprovedAsync(approved, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<ApplicationRelationshipView>> ListActiveIncomingAsync(
        Uuid tenantId, Uuid targetApplicationId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct)
            .ConfigureAwait(false);
        return await ApplicationIncomingRelationshipSchema.Relationships.QueryAsync(tx,
            ApplicationIncomingRelationshipSchema.ByTargetApplication.Query()
                .WithPrefix(targetApplicationId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(
            ApplicationIncomingRelationshipProjector.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "applications")), ct);

    async ValueTask ApplyRecordedAsync(ApplicationRelationshipRecorded ev,
        CancellationToken ct)
    {
        ValidateScope(ev.TenantId, ev.RelationshipId, ev.SourceApplicationId,
            ev.TargetApplicationId, ev.RelationshipType);
        var expectedStatus = ev.RelationshipType == ApplicationRelationshipIdentity.Replaces
            ? "proposed"
            : "active";
        if (ev.Revision < 1 || ev.SourceApplicationRevision < 1 ||
            ev.TargetApplicationRevision < 1 || ev.Status != expectedStatus ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ActorDisplay) ||
            ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship record is invalid.");

        var current = await ApplicationIncomingRelationshipSchema.Relationships
            .GetAsync(Transaction, ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = new ApplicationRelationshipView(ev.RelationshipId,
            ev.TargetApplicationId, ev.RelationshipType, ev.ActorMemberId,
            ev.ActorDisplay, ev.ChangedAt)
        {
            TenantId = ev.TenantId,
            SourceApplicationId = ev.SourceApplicationId,
            Revision = ev.Revision,
            Status = ev.Status,
            ChangedBy = ev.Actor,
            SourceApplicationRevision = ev.SourceApplicationRevision,
            TargetApplicationRevision = ev.TargetApplicationRevision,
        };
        if (current == expected)
            return;
        if (current is null)
        {
            if (ev.Revision != 1)
                throw new InvalidOperationException(
                    "An application relationship record is out of order.");
            await ApplicationIncomingRelationshipSchema.Relationships.InsertAsync(Transaction,
                expected, ct).ConfigureAwait(false);
            return;
        }
        if (current.TenantId != ev.TenantId ||
            current.SourceApplicationId != ev.SourceApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ev.RelationshipType || current.Status != "removed" ||
            ev.Revision != current.Revision + 1)
            throw new InvalidOperationException(
                "An application relationship record is out of order.");
        await ApplicationIncomingRelationshipSchema.Relationships.ReplaceAsync(Transaction,
            current, expected, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyLegacyAddedAsync(ApplicationRelationshipAdded ev,
        CancellationToken ct)
    {
        ValidateLegacyScope(ev.TenantId, ev.ApplicationId, ev.RelationshipId,
            ev.TargetApplicationId, ev.RelationshipType);
        if (ev.RelationshipRevision < 1 || ev.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ActorDisplay) || ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship add is invalid.");

        var current = await ApplicationIncomingRelationshipSchema.Relationships
            .GetAsync(Transaction, ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = new ApplicationRelationshipView(ev.RelationshipId,
            ev.TargetApplicationId, ev.RelationshipType, ev.ActorMemberId,
            ev.ActorDisplay, ev.ChangedAt)
        {
            TenantId = ev.TenantId,
            SourceApplicationId = ev.ApplicationId,
            Revision = ev.RelationshipRevision,
            Status = "active",
            ChangedBy = ev.Actor,
        };
        if (current == expected)
            return;
        if (current is not null)
            throw new InvalidOperationException("An application relationship add is out of order.");
        await ApplicationIncomingRelationshipSchema.Relationships.InsertAsync(Transaction,
            expected, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyLegacyRemovedAsync(ApplicationRelationshipRemoved ev,
        CancellationToken ct)
    {
        ValidateLegacyScope(ev.TenantId, ev.ApplicationId, ev.RelationshipId,
            ev.TargetApplicationId, ev.RelationshipType);
        if (ev.RelationshipRevision < 2 || ev.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ActorDisplay) || ev.ChangedAt == default)
            throw new InvalidOperationException(
                "An application relationship removal is invalid.");
        var current = await RequireCurrentAsync(ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = current with
        {
            Revision = ev.RelationshipRevision,
            Status = "removed",
            ChangedBy = ev.Actor,
            ActorMemberId = ev.ActorMemberId,
            ActorDisplay = ev.ActorDisplay,
            ChangedAt = ev.ChangedAt,
            RemovalReason = ev.Reason,
            Approval = null,
        };
        if (current.Revision == ev.RelationshipRevision && current == expected)
            return;
        if (current.TenantId != ev.TenantId ||
            current.SourceApplicationId != ev.ApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ev.RelationshipType || current.Status == "removed" ||
            ev.RelationshipRevision <= current.Revision)
            throw new InvalidOperationException(
                "An application relationship removal is out of order.");
        await ApplicationIncomingRelationshipSchema.Relationships.ReplaceAsync(Transaction,
            current, expected, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyRemovedAsync(ApplicationRelationshipGovernedRemoved ev,
        CancellationToken ct)
    {
        ValidateScope(ev.TenantId, ev.RelationshipId, ev.SourceApplicationId,
            ev.TargetApplicationId, ev.RelationshipType);
        if (ev.Revision < 2 || string.IsNullOrWhiteSpace(ev.Reason) || ev.Reason.Length > 2000 ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ActorDisplay) ||
            ev.ChangedAt == default)
            throw new InvalidOperationException(
                "An application relationship removal is invalid.");
        var current = await RequireCurrentAsync(ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = current with
        {
            Revision = ev.Revision,
            Status = "removed",
            ChangedBy = ev.Actor,
            ActorMemberId = ev.ActorMemberId,
            ActorDisplay = ev.ActorDisplay,
            ChangedAt = ev.ChangedAt,
            RemovalReason = ev.Reason,
            Approval = null,
        };
        if (current.Revision == ev.Revision && current == expected)
            return;
        if (current.TenantId != ev.TenantId ||
            current.SourceApplicationId != ev.SourceApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ev.RelationshipType || current.Status == "removed" ||
            ev.Revision != current.Revision + 1)
            throw new InvalidOperationException(
                "An application relationship removal is out of order.");
        await ApplicationIncomingRelationshipSchema.Relationships.ReplaceAsync(Transaction,
            current, expected, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyApprovedAsync(ApplicationRelationshipApproved ev,
        CancellationToken ct)
    {
        ValidateScope(ev.TenantId, ev.RelationshipId, ev.SourceApplicationId,
            ev.TargetApplicationId, ApplicationRelationshipIdentity.Replaces);
        if (ev.Revision < 2 || ev.SourceApplicationRevision < 1 ||
            ev.TargetApplicationRevision < 1 ||
            !ApplicationRelationshipIdentity.IsImpactDigest(ev.ImpactDigest) ||
            ev.ApproverMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ApproverDisplay) ||
            ev.ApprovedAt == default)
            throw new InvalidOperationException("An application relationship approval is invalid.");
        var current = await RequireCurrentAsync(ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = current with
        {
            Revision = ev.Revision,
            Status = "approved",
            ChangedBy = ev.Approver,
            ActorMemberId = ev.ApproverMemberId,
            ActorDisplay = ev.ApproverDisplay,
            ChangedAt = ev.ApprovedAt,
            SourceApplicationRevision = ev.SourceApplicationRevision,
            TargetApplicationRevision = ev.TargetApplicationRevision,
            RemovalReason = null,
            Approval = new ApplicationRelationshipApproval(ev.ApproverMemberId,
                ev.Approver, ev.SourceApplicationRevision, ev.TargetApplicationRevision,
                ev.ImpactDigest, ev.ApprovedAt),
        };
        if (current.Revision == ev.Revision && current == expected)
            return;
        if (current.TenantId != ev.TenantId ||
            current.SourceApplicationId != ev.SourceApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ApplicationRelationshipIdentity.Replaces ||
            current.Status != "proposed" || ev.Revision != current.Revision + 1 ||
            current.SourceApplicationRevision != ev.SourceApplicationRevision ||
            current.TargetApplicationRevision != ev.TargetApplicationRevision)
            throw new InvalidOperationException(
                "An application relationship approval is out of order.");
        await ApplicationIncomingRelationshipSchema.Relationships.ReplaceAsync(Transaction,
            current, expected, ct).ConfigureAwait(false);
    }

    async ValueTask<ApplicationRelationshipView> RequireCurrentAsync(Uuid relationshipId,
        CancellationToken ct) => await ApplicationIncomingRelationshipSchema.Relationships
        .GetAsync(Transaction, relationshipId, ct).ConfigureAwait(false) ??
        throw new InvalidOperationException(
            "An application relationship change cannot project before its record.");

    static void ValidateScope(Uuid tenantId, Uuid relationshipId, Uuid sourceApplicationId,
        Uuid targetApplicationId, string relationshipType)
    {
        if (tenantId == Uuid.Empty || relationshipId == Uuid.Empty ||
            sourceApplicationId == Uuid.Empty || targetApplicationId == Uuid.Empty ||
            sourceApplicationId == targetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            relationshipId != ApplicationRelationshipIdentity.Id(sourceApplicationId,
                relationshipType, targetApplicationId))
            throw new InvalidOperationException("An application relationship scope is invalid.");
    }

    static void ValidateLegacyScope(Uuid tenantId, Uuid sourceApplicationId,
        Uuid relationshipId, Uuid targetApplicationId, string relationshipType)
    {
        if (tenantId == Uuid.Empty || sourceApplicationId == Uuid.Empty ||
            relationshipId == Uuid.Empty || targetApplicationId == Uuid.Empty ||
            sourceApplicationId == targetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(relationshipType))
            throw new InvalidOperationException("An application relationship scope is invalid.");
    }
}
