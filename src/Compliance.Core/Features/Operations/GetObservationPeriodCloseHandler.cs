using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Resolves one unambiguous scoped historical close from native identity discovery and exact source-backed period lineage.</summary>
public sealed class GetObservationPeriodCloseHandler(IObservationPeriodCloseDirectoryReader directory,
    ObservationPeriodDirectoryReadConsistency consistency, TypeIIPeriodReader periods)
    : IRequestHandler<GetObservationPeriodClose, ObservationPeriodCloseView>
{
    public async ValueTask<Result<ObservationPeriodCloseView>> HandleAsync(IRequestContext<GetObservationPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ObservationPeriodCloseView>.Failure(fence.Error!);
        var identities = await directory.FindAsync(request.TenantId, request.ProgramId, request.ObservationPeriodCloseId, ct).ConfigureAwait(false);
        if (identities.Any(row => row.TenantId != request.TenantId || row.ProgramId != request.ProgramId || row.CloseId != request.ObservationPeriodCloseId))
            return Conflict("The close directory contains another tenant, program or close identity.");
        if (identities.Count > 1)
            return Conflict("The close identity is ambiguous across periods in this program.");
        Result<ObservationPeriodCloseView> result;
        if (identities.Count == 0)
            result = Result<ObservationPeriodCloseView>.Failure(new RequestError(RequestErrorKind.NotFound, "The frozen close was not found in this program."));
        else
        {
            var identity = identities[0];
            var read = await periods.TryReadAsync(request.TenantId, request.ProgramId, identity.PeriodId, ct).ConfigureAwait(false);
            if (!read.IsSuccess)
                return Result<ObservationPeriodCloseView>.Failure(read.Error!);
            var state = read.Value.View;
            var snapshot = state?.CloseSnapshots.SingleOrDefault(close => close.CloseSnapshotId == identity.CloseId);
            var fact = read.Value.FactById(identity.Source.EventId) as ITypeIIPeriodStateFact;
            if (state is null || snapshot is null || fact is null)
                return Conflict("The exact native close source is not visible yet.", true);
            if (identity.Source.Stream != read.Value.Stream || fact.Header.ProgramId != request.ProgramId ||
                fact.Header.LatestCloseSnapshotId != identity.CloseId || fact.Header.LatestCloseVersion != identity.Version ||
                fact.Revision != identity.PeriodRevision || snapshot.Version != identity.Version ||
                ContentDigest.OfJson(snapshot) != identity.SnapshotSha256 ||
                read.Value.FactById(identity.Source.EventId)!.Metadata!.AggregateVersion != identity.Source.AggregateVersion)
                return Conflict("The close locator differs from its exact authoritative historical snapshot.");
            result = Result<ObservationPeriodCloseView>.Success(new ObservationPeriodCloseView(request.TenantId, request.ProgramId,
                identity.CloseId, state.PeriodId, state.ExaminationId, snapshot, state.CloseSnapshots));
        }
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, fence.Value, ct).ConfigureAwait(false);
        return confirmed.IsSuccess ? result : Result<ObservationPeriodCloseView>.Failure(confirmed.Error!);
    }
    static Result<ObservationPeriodCloseView> Conflict(string message, bool transient = false) =>
        Result<ObservationPeriodCloseView>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
