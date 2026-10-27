using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Materializes the immutable risk acceptance link from its admission on the gap. <see cref="Source" /> is the
///     admission fact; the receiver verifies it by hydrating the gap and never changes the gap.
/// </summary>
[Discriminator("bdgrz.provider-risk-acceptance.materialize-provider-risk-acceptance-link", 1)]
public sealed record MaterializeProviderRiskAcceptanceLink(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid GapId, Uuid LinkId) : IRequest<ReceiverOutcome>, IIdentifiedRequest, IProviderAssuranceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
