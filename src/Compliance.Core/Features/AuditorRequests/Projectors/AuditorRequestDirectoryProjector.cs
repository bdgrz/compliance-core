using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Projections;
namespace Bdgrz.Compliance.Features.AuditorRequests.Projectors;

public sealed partial class AuditorRequestDirectoryProjector(IAuditorRequestDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AuditorRequest.Area), FitzAuditorRequestDirectory.ProjectorName),
        IProjectorHandler<AuditorRequestRecorded>, IProjectorHandler<AuditorSampleSelectionAdmitted>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<AuditorRequestRecorded> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        await projection.ApplyAsync(fact, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AuditorSampleSelectionAdmitted> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        await projection.ApplyAsync(fact, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
