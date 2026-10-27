using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     A provider coverage gap was recorded as the first revision of its own owner. The complete
///     <see cref="ProviderCoverageGapContent" /> is immutable content; <paramref name="RequestId" /> is the gap id.
/// </summary>
[Discriminator("bdgrz.provider.coverage_gap.recorded", 1)]
public sealed record ProviderCoverageGapRecorded(Uuid TenantId, Uuid GapId, Uuid ProviderId, Uuid RequestId,
    long ProviderRevision, ImmutableContentRef ContentContent, ActorReference Actor, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
