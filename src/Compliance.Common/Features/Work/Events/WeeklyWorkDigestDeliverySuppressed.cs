using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>Records why a scheduled digest was suppressed before an email attempt began.</summary>
[Discriminator("bdgrz.work.weekly-digest.delivery-suppressed", 2)]
public sealed record WeeklyWorkDigestDeliverySuppressed(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    long Revision,
    string Reason,
    ActorReference SuppressedBy,
    DateTimeOffset SuppressedAt) : DomainEvent, IWeeklyWorkDigestOperationFact
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
