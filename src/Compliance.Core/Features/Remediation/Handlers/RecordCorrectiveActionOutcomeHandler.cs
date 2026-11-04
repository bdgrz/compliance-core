using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Verifies a terminal action source before recording its outcome on the finding.</summary>
public sealed class RecordCorrectiveActionOutcomeHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RecordCorrectiveActionOutcome>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordCorrectiveActionOutcome> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only an action reaction may report its terminal outcome."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.FindingId == Uuid.Empty ||
            request.ActionId == Uuid.Empty || request.Outcome is not ("completed" or "cancelled") ||
            request.Source is null || request.Source.EventId == Uuid.Empty ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(RecordCorrectiveActionOutcome)))
            return Invalid();
        var action = new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId, request.ActionId);
        if (request.Source.Stream != action.Stream)
            return Invalid();
        try
        {
            var proof = request.Outcome == "completed"
                ? await VerifyCompletionAsync(request, action, ct).ConfigureAwait(false)
                : await VerifyCancellationAsync(request, action, ct).ConfigureAwait(false);
            if (proof is null)
                return Invalid();
            var finding = new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId);
            var operation = FindingContent.Provenance(request.CommandId, nameof(RecordCorrectiveActionOutcome),
                finding.Stream, ActorReference.ForSystemProcess("report_corrective_action_outcome", "Report corrective action outcome"),
                null, FindingCommands.Digest(request), clock.GetUtcNow()) with
            { FirstOwner = finding.Stream, CausationEventId = request.Source.EventId };
            return await executor.ExecuteAsync(finding, owner => AggregateOutcome.CommitOnSuccess(
                owner.RecordActionOutcome(operation, request.ActionId, request.Outcome, request.Source, proof)),
                context, ct).ConfigureAwait(false);
        }
        catch (SourceFactVerificationException error)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The action outcome source cannot be verified: {error.Message}", error is SourceFactUnavailableException));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The action outcome proof cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async Task<ImmutableContentRef?> VerifyCompletionAsync(RecordCorrectiveActionOutcome request,
        CorrectiveAction action, CancellationToken ct)
    {
        var (proof, fact, _) = await SourceFactProofs.BuildAsync<CorrectiveActionCompletedV2>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        return fact.TenantId == request.TenantId && fact.ProgramId == request.ProgramId &&
               fact.FindingId == request.FindingId && fact.ActionId == request.ActionId &&
               fact.Metadata.AggregateId == action.Id && fact.Operation.Owner == action.Stream
            ? proof.RequiredFacts
            : null;
    }

    async Task<ImmutableContentRef?> VerifyCancellationAsync(RecordCorrectiveActionOutcome request,
        CorrectiveAction action, CancellationToken ct)
    {
        var (proof, fact, _) = await SourceFactProofs.BuildAsync<CorrectiveActionCancelled>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        return fact.TenantId == request.TenantId && fact.ProgramId == request.ProgramId &&
               fact.FindingId == request.FindingId && fact.ActionId == request.ActionId &&
               fact.Metadata.AggregateId == action.Id && fact.Operation.Owner == action.Stream
            ? proof.RequiredFacts
            : null;
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "An outcome report requires the exact committed completion or cancellation of its planned action."));
}
