using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Changes.Events;

/// <summary>One compact significant-change fact and its exact immutable local mutation.</summary>
[Discriminator("bdgrz.changes.significant-change-impact-assessed", 3)]
public sealed record SignificantChangeImpactAssessed(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    SignificantChangeAuthorityHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Change,
    long NativeSequence, int ChangeCount, OperationProvenance Operation, ImmutableContentRef FirstResponse,
    ImmutableContentRef OperationResult, DateTimeOffset OccurredAt) : DomainEvent, ISignificantChangeFact;
