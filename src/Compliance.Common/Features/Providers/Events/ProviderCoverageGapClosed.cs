using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     The open provider coverage gap was closed once, against a newer or different source. The complete
///     <see cref="ProviderCoverageGapClosureContent" /> is immutable content. A closure never erases an already
///     admitted risk link.
/// </summary>
[Discriminator("bdgrz.provider.coverage_gap.closed", 1)]
public sealed record ProviderCoverageGapClosed(Uuid TenantId, Uuid GapId, Uuid ProviderId, Uuid RequestId,
    long Revision, ImmutableContentRef ContentContent, ActorReference Actor, DateTimeOffset ClosedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
