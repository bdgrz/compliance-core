using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>
///     Lists a control's occurrence population: recorded occurrences plus expected periods that
///     start within the next 90 days, ordered by due date.
/// </summary>
public sealed class ListControlOccurrencesHandler(ControlReader controlSources, ControlOperationsReader operations,
    TimeProvider clock)
    : IRequestHandler<ListControlOccurrences, Page<ControlOccurrenceView>>
{
    const int HorizonDays = 90;

    public ValueTask<Result<Page<ControlOccurrenceView>>> HandleAsync(
        IRequestContext<ListControlOccurrences> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<Page<ControlOccurrenceView>>> HandleCoreAsync(
        IRequestContext<ListControlOccurrences> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<Page<ControlOccurrenceView>>.Failure(
                ControlOperationsSource.ControlNotFound());
        var snapshot = await operations.ReadControlAsync(request.TenantId, request.ProgramId, request.ControlId, ct)
            .ConfigureAwait(false);
        var today = ControlOperationsSource.Today(clock);
        var items = snapshot.ReadOccurrences(request.ControlId,
                OperatingAuthority.VersionWindows(control), today, today.AddDays(HorizonDays))
            .Where(view => request.State is null || view.State == request.State)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "control occurrences");
    }
}
