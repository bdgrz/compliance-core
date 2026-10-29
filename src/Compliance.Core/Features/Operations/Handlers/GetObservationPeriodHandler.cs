using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Queries;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Returns current scoped period authority after its native opening has applied.</summary>
public sealed class GetObservationPeriodHandler(IAggregateReader reader)
    : IRequestHandler<GetObservationPeriod, ObservationPeriodView>
{
    public ValueTask<Result<ObservationPeriodView>> HandleAsync(IRequestContext<GetObservationPeriod> context, CancellationToken ct)
    {
        var request = context.Request;
        return ObservationPeriodRead.GetAsync(reader, request.TenantId, request.ProgramId, request.ObservationPeriodId, ct);
    }
}
