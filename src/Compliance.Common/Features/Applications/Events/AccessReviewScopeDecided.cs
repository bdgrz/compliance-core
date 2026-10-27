using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>An attributable immutable personal scope decision at the exact named instance revision.</summary>
[Discriminator("bdgrz.system_instance.access_review_scope.decided", 2)]
public sealed record AccessReviewScopeDecided(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId,
    long SystemInstanceRevision, Uuid DecisionId, long Sequence, DateTimeOffset EffectiveFrom,
    ImmutableContentRef Content, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
