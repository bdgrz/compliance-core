using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Verifies the retirement source before cancelling only still-open future work.</summary>
public sealed class CancelRetiredControlOccurrenceHandler(IAggregateExecutor executor,
    ISourceFactResolver sources, TimeProvider clock) : IRequestHandler<CancelRetiredControlOccurrence>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CancelRetiredControlOccurrence> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal retirement reaction may cancel future occurrences."));
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0 || request.Retirement is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The retirement cancellation has no exact source."));
        ControlRetired retirement;
        try
        {
            retirement = (await sources.VerifyAsync(request.Retirement, ct).ConfigureAwait(false)).Facts;
        }
        catch (SourceFactVerificationException failure)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact retirement source cannot be verified.", failure is SourceFactUnavailableException));
        }
        var source = new ControlDraft(request.TenantId, request.ControlId).Stream;
        if (request.Retirement.Source != source || retirement.TenantId != request.TenantId ||
            retirement.ProgramId != request.ProgramId || retirement.ControlId != request.ControlId ||
            retirement.ChangeCount == 0 || retirement.Operation.Owner != source)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The retirement source belongs to another control."));
        var owner = new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId,
            request.OccurrenceId, request.CommandId);
        var provenance = new OperationProvenance(request.CommandId, retirement.Operation.RootOperationId,
            request.Retirement.EventId, nameof(CancelRetiredControlOccurrence), retirement.Actor, owner.Stream,
            retirement.Operation.Episode, (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request),
            clock.GetUtcNow(), retirement.Operation.FirstOwner ?? source, owner.Stream, owner.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        return await executor.ExecuteAsync(owner, current =>
        {
            switch (current.Acceptance.Decide(binding))
            {
                case NativeAcceptanceDecision.Conflict:
                    return Result.Failure(AcceptedAppendResult.ConflictError);
                case NativeAcceptanceDecision.Replayed:
                    return Result.Success;
            }
            if (current.Opened is not { BusinessWindow: ControlCalendarWindow window } ||
                window.From < retirement.EffectiveUntil ||
                current.Latest is not ControlOccurrenceOpened &&
                current.Latest is not ControlOccurrenceReviewed { Outcome: "returned" })
                return Result.Success;
            return current.Cancel(provenance, retirement.Rationale, null, null);
        }, context, ct).ConfigureAwait(false);
    }
}
