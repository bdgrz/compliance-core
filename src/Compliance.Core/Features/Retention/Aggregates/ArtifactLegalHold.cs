using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Aggregates;

/// <summary>One indefinite legal hold and its one exact release, bound forever to the placed content.</summary>
public sealed class ArtifactLegalHold : Aggregate
{
    public const string Area = "artifact-legal-holds";
    readonly Uuid _tenantId;

    public ArtifactLegalHold(Uuid tenantId, Uuid holdId, Uuid selectedLogicalId = default)
        : base(holdId, new EventStreamAddress(tenantId.ToString(), Area, holdId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ArtifactLegalHoldPlaced>(Apply);
        On<ArtifactLegalHoldReleased>(Apply);
    }

    public NativeAcceptance Acceptance { get; }
    public ArtifactLegalHoldPlaced? Placement { get; private set; }
    public ArtifactLegalHoldReleased? Release { get; private set; }
    public long Revision => Release is not null ? 2 : Placement is not null ? 1 : 0;
    public ArtifactRetentionSource? Source => Placement?.Source;
    public ArtifactLegalHoldView? View => Placement is { } placed
        ? new(Id, placed.Reason, placed.RecordedBy, placed.RecordedAt,
            Release?.Reason, Release?.RecordedBy, Release?.RecordedAt)
        : null;

    public Result PreparePlace(ArtifactRetentionSourceSnapshot source, long expectedRevision, string reason)
    {
        if (!ValidSource(source) || expectedRevision < 0 || !ArtifactRetentionRules.ValidReason(reason))
            return ArtifactRetentionRules.Invalid("A legal hold requires exact durable content and a bounded reason.");
        return expectedRevision == 0 && Placement is null ? Result.Success :
            ArtifactRetentionRules.Conflict("The hold identity is already placed or its original revision changed.");
    }

    public Result PrepareRelease(ArtifactRetentionSourceSnapshot source, long expectedRevision, string reason,
        DateTimeOffset at)
    {
        if (!ValidSource(source) || expectedRevision < 0 || !ArtifactRetentionRules.ValidReason(reason) || at == default)
            return ArtifactRetentionRules.Invalid("A release requires exact durable content and a bounded reason.");
        if (Placement is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The legal hold was not found."));
        if (source.Source != Placement.Source || source.Position < Placement.SourcePosition)
            return ArtifactRetentionRules.Conflict("The release source differs from or precedes the exact held content.");
        if (Release is not null || expectedRevision != 1)
            return ArtifactRetentionRules.Conflict("The legal hold is released or its revision changed.");
        return at >= Placement.RecordedAt ? Result.Success :
            ArtifactRetentionRules.Invalid("A release cannot precede the placement.");
    }

    public Result Place(OperationProvenance operation, ArtifactRetentionSourceSnapshot source, string reason,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (ArtifactRetentionRules.Replay(Acceptance, operation) is { } retry)
            return retry;
        if (operation.LogicalId != Id ||
            !ArtifactRetentionRules.ValidOperation(operation, Stream, "PlaceArtifactLegalHold", 0))
            return ArtifactRetentionRules.Invalid("The hold identity is its exact native creation operation.");
        var prepared = PreparePlace(source, 0, reason);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ArtifactLegalHoldPlaced(source.Source, 1, source.Position, Id, reason.Trim(), operation.Actor,
            operation.AcceptedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    public Result ReleaseHold(OperationProvenance operation, ArtifactRetentionSourceSnapshot source, string reason,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (ArtifactRetentionRules.Replay(Acceptance, operation) is { } retry)
            return retry;
        if (!ArtifactRetentionRules.ValidOperation(operation, Stream, "ReleaseArtifactLegalHold", 1))
            return ArtifactRetentionRules.Invalid("A hold release is its exact native operation at the original revision.");
        var prepared = PrepareRelease(source, 1, reason, operation.AcceptedAt);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ArtifactLegalHoldReleased(source.Source, 2, source.Position, Id, reason.Trim(), operation.Actor,
            operation.AcceptedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    bool ValidSource(ArtifactRetentionSourceSnapshot source) => Id != Uuid.Empty && _tenantId != Uuid.Empty &&
        ArtifactRetentionRules.ValidSource(source.Source) && source.Source.TenantId == _tenantId && source.Position > 0;

    void Apply(ArtifactLegalHoldPlaced ev)
    {
        if (Placement is not null || ev.HoldId != Id || ev.Revision != 1 || ev.SourcePosition == 0 ||
            !ValidSource(new(ev.Source, ev.SourcePosition)) || ev.Operation.LogicalId != Id ||
            !ValidFact(ev.Operation, ev.RecordedBy, ev.RecordedAt, ev.Reason, "PlaceArtifactLegalHold", 0) ||
            ev.FirstResponse is null || ev.OperationResult is null)
            throw new InvalidOperationException("A legal hold is one exact attributed native placement.");
        Acceptance.Observe(ev);
        Placement = ev;
    }

    void Apply(ArtifactLegalHoldReleased ev)
    {
        if (Placement is null || Release is not null || ev.HoldId != Id || ev.Revision != 2 ||
            ev.Source != Placement.Source || ev.SourcePosition < Placement.SourcePosition ||
            ev.RecordedAt < Placement.RecordedAt ||
            !ValidFact(ev.Operation, ev.RecordedBy, ev.RecordedAt, ev.Reason, "ReleaseArtifactLegalHold", 1) ||
            ev.FirstResponse is null || ev.OperationResult is null)
            throw new InvalidOperationException("A legal hold has one exact native release of its placed content.");
        Acceptance.Observe(ev);
        Release = ev;
    }

    bool ValidFact(OperationProvenance operation, ActorReference actor, DateTimeOffset at, string reason,
        string kind, long expected) => operation.Actor == actor && operation.AcceptedAt == at &&
        ArtifactRetentionRules.ValidReason(reason) && reason == reason.Trim() &&
        ArtifactRetentionRules.ValidOperation(operation, Stream, kind, expected);
}
