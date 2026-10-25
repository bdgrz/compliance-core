using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Raises the finding for a failed or skipped attestation or a reviewer's requested action.
///     The control owner (or the recorder when the owner is not a member) owns it, due in 30 days
///     at medium severity as a product default the compliance lead may revise. A requested action
///     also becomes the finding's corrective action, admitted atomically with the raise. Replays are idempotent by
///     finding ID.
/// </summary>
public sealed class RaiseOccurrenceFindingHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ControlOperationsReader operatingReader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RaiseOccurrenceFinding>
{
    public const int DefaultDueDays = RoutedFinding.DefaultDueDays;
    public const string DefaultSeverity = RoutedFinding.DefaultSeverity;
    public const string ProcessId = "reactor:" + ControlOccurrenceFindingReactor.WorkloadName;

    public async ValueTask<Result> HandleAsync(IRequestContext<RaiseOccurrenceFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var actor = ActorReference.ForSystemProcess(ProcessId, ControlOccurrenceFindingReactor.WorkloadName);
        async ValueTask<Result<RoutedFindingSource>> PrepareSourceAsync(CancellationToken token)
        {
            var snapshot = await operatingReader.ReadOccurrenceAsync(request.TenantId, request.ProgramId, request.ControlId,
                request.OccurrenceId, token).ConfigureAwait(false);
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (snapshot.ReadOccurrence(request.ControlId, request.OccurrenceId,
                    new Dictionary<Uuid, DateOnly?>(), today) is not { Revision: > 0 } occurrence ||
                occurrence.Attestations.Count == 0)
                return Result<RoutedFindingSource>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The control occurrence was not found."));
            var owner = snapshot.CurrentPlan(request.ControlId)?.Owner is { Kind: "member" } member
                ? member.Id
                : occurrence.Reviews.Count > 0
                    ? occurrence.Reviews[^1].ReviewerMemberId
                    : occurrence.Attestations[^1].RecorderMemberId;
            var requestedAction = request.SourceKind == "occurrence_review";
            return Result<RoutedFindingSource>.Success(new RoutedFindingSource(
                new FindingSource(request.SourceKind, request.OccurrenceId, request.SourceVersion, request.SourceText),
                requestedAction ? "Requested action from control review" : "Control occurrence exception",
                $"Control {request.ControlId}, occurrence {request.OccurrenceId}", owner,
                [new FindingLink("control", request.ControlId.ToString()),
                    new FindingLink("control_occurrence", request.OccurrenceId.ToString())],
                requestedAction ? request.SourceText : null));
        }
        return await RoutedFinding.RaiseFromSourceAsync(operations, reader, content, context, "RaiseOccurrenceFinding",
            request, request.TenantId, request.ProgramId, request.FindingId, actor, now, PrepareSourceAsync, ct)
            .ConfigureAwait(false);
    }
}
