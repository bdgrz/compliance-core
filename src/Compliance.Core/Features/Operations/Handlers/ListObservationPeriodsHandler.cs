using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

public sealed class ListObservationPeriodsHandler(IObservationPeriodDirectoryReader directory,
    ObservationPeriodDirectoryReadConsistency consistency, IAggregateReader reader)
    : IRequestHandler<ListObservationPeriods, Page<ObservationPeriodSummaryView>>
{
    public async ValueTask<Result<Page<ObservationPeriodSummaryView>>> HandleAsync(IRequestContext<ListObservationPeriods> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ObservationPeriodSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Limit must be between 1 and 200."));
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<ObservationPeriodSummaryView>>.Failure(fence.Error!);
        Page<ObservationPeriodIdentity> identities;
        try
        {
            identities = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ObservationPeriodSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The observation-period cursor is invalid."));
        }
        if (identities.Items.Any(value => value.TenantId != request.TenantId || value.ProgramId != request.ProgramId))
            return Result<Page<ObservationPeriodSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The observation-period directory contains another scope."));
        var current = new List<ObservationPeriodSummaryView>(identities.Items.Count);
        foreach (var identity in identities.Items)
        {
            var package = await ObservationPeriodRead.GetAsync(reader, request.TenantId, request.ProgramId,
                identity.ObservationPeriodId, ct).ConfigureAwait(false);
            if (!package.IsSuccess)
                return Result<Page<ObservationPeriodSummaryView>>.Failure(package.Error!.Kind == RequestErrorKind.NotFound
                    ? new RequestError(RequestErrorKind.Conflict, "A listed observation period is not visible at its source yet.", true)
                    : package.Error);
            var view = package.Value;
            current.Add(new ObservationPeriodSummaryView(view.TenantId, view.ProgramId, view.ObservationPeriodId,
                view.ExaminationId, view.Status, view.Revision, view.PeriodStart, view.PeriodEnd, view.UpdatedAt));
        }
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, fence.Value, ct)
            .ConfigureAwait(false);
        return confirmed.IsSuccess
            ? Result<Page<ObservationPeriodSummaryView>>.Success(new Page<ObservationPeriodSummaryView>(current, identities.NextCursor))
            : Result<Page<ObservationPeriodSummaryView>>.Failure(confirmed.Error!);
    }
}
