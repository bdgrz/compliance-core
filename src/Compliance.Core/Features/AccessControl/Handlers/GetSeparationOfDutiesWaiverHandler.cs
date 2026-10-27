using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Queries;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetSeparationOfDutiesWaiverHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>
{
    public async ValueTask<Result<SeparationOfDutiesWaiverView>> HandleAsync(
        IRequestContext<GetSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        var request = context.Request;
        var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
            request.WaiverId), ct).ConfigureAwait(false);
        return waiver.ViewOrNull(clock.GetUtcNow()) is { } view
            ? Result<SeparationOfDutiesWaiverView>.Success(view)
            : Result<SeparationOfDutiesWaiverView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The separation-of-duties waiver was not found."));
    }
}
