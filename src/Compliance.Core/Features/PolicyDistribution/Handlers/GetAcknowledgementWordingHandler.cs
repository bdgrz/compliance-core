using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

public sealed class GetAcknowledgementWordingHandler(IAggregateReader reader)
    : IRequestHandler<GetAcknowledgementWording, AcknowledgementWordingView>
{
    public async ValueTask<Result<AcknowledgementWordingView>> HandleAsync(
        IRequestContext<GetAcknowledgementWording> context, CancellationToken ct)
    {
        if (context.Request.TenantId == Uuid.Empty)
            return Result<AcknowledgementWordingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An organization identity is required."));
        var owner = await reader.HydrateAsync(new AcknowledgementWording(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<AcknowledgementWordingView>.Success(owner.View());
    }
}
