using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>Records the durable outcome of one exact weekly digest delivery attempt.</summary>
[Discriminator("bdgrz.work.weekly-digest.delivery-recorded", 2)]
public sealed record WeeklyWorkDigestDeliveryRecorded(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    long Revision,
    int AttemptNumber,
    string DeliveryKey,
    string Outcome,
    string? FailureCode,
    ActorReference RecordedBy,
    DateTimeOffset AttemptedAt) : DomainEvent, IWeeklyWorkDigestOperationFact
{
    public OperationProvenance Operation { get; init; } = null!;
    public RecordWorkDigestDelivery Authored { get; init; } = null!;
    public ActorAttribution OriginalActor { get; init; } = null!;
    public SourceFactProof<WeeklyWorkDigestDeliveryRequested> SourceProof { get; init; } = null!;
    public bool NoChange { get; init; }
    public ImmutableContentRef Change { get; init; } = null!;
    public ImmutableContentRef ResponseContent { get; init; } = null!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
}
