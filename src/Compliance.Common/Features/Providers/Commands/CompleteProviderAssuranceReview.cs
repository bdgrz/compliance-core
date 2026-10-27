using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Records the admitted review as an immutable fact once every source use it requires is held.
///     <paramref name="ReportUseProof" /> is the immutable list of grant facts the acquire stage produced, in the
///     order of the frozen requirement manifest.
/// </summary>
[Discriminator("bdgrz.provider-assurance-review.complete-provider-assurance-review", 1)]
public sealed record CompleteProviderAssuranceReview(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid ReviewId, ImmutableContentRef ReportUseProof) : IRequest<ReceiverOutcome>, IIdentifiedRequest,
    IProviderAssuranceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
