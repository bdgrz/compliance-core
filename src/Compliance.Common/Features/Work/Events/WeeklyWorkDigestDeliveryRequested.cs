using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>Records one deterministic email delivery attempt for a scheduled weekly digest.</summary>
[Discriminator("bdgrz.work.weekly-digest.delivery-requested", 2)]
public sealed record WeeklyWorkDigestDeliveryRequested(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    long Revision,
    int AttemptNumber,
    string DeliveryKey,
    ActorReference RequestedBy,
    DateTimeOffset RequestedAt) : DomainEvent, IWeeklyWorkDigestOperationFact
{
    public OperationProvenance Operation { get; init; } = null!;
    public DeliverWeeklyWorkDigest Authored { get; init; } = null!;
    public ActorAttribution OriginalActor { get; init; } = null!;
    public SourceFactProof<WeeklyWorkDigestScheduled> SourceProof { get; init; } = null!;
    public bool NoChange { get; init; }
    public ImmutableContentRef Change { get; init; } = null!;
    public ImmutableContentRef ResponseContent { get; init; } = null!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
}
