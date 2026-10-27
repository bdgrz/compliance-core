using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     The immutable accepted-risk link materialized from its admission. It belongs only to the link id, never
///     changes the gap, and keeps the admission revision and instant even if the gap later closes or the acceptance
///     expires. <paramref name="AcceptanceContent" /> is the <see cref="ProviderCoverageGapRiskAcceptanceView" />.
/// </summary>
[Discriminator("bdgrz.provider.coverage_gap.risk_acceptance_linked", 1)]
public sealed record ProviderCoverageGapRiskAcceptanceLinked(Uuid TenantId, Uuid GapId, Uuid ProviderId,
    Uuid RequestId, long Revision, ImmutableContentRef AcceptanceContent, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
