using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     A risk acceptance link was admitted on the open gap at its expected revision. <paramref name="Revision" /> is
///     the gap revision this admission produced; <see cref="OperationProvenance.Episode" /> carries the admitted
///     <c>RiskId:AcceptanceId</c> pair, which the gap serializes for uniqueness. <paramref name="Intent" /> is the
///     frozen <see cref="ProviderRiskLinkIntent" />.
/// </summary>
[Discriminator("bdgrz.provider-assurance-gap.provider-coverage-gap-risk-acceptance-admitted", 1)]
public sealed record ProviderCoverageGapRiskAcceptanceAdmitted(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
