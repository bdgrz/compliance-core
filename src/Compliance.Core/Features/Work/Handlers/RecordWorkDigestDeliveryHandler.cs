using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Processes;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Records only the exact admitted attempt, with original native result recovery before live guard reads.</summary>
public sealed class RecordWorkDigestDeliveryHandler(WeeklyDigestOperations operations) : IRequestHandler<RecordWorkDigestDelivery>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordWorkDigestDelivery> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => operations.RecordAsync(context, ct));
}
