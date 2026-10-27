using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Admits owned corrective work on a finding. The complete action intent and the finding generation are frozen on
///     the finding before the 202 acknowledgment is frozen on the admission, so an exact retry replays the original
///     acknowledgment even after the finding changed; <c>IndependentChildMaterialization</c> then materializes the
///     action owner and the action enters its owner's work queue. A finding that is closing or closed admits no action.
/// </summary>
public sealed class AddCorrectiveActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AddCorrectiveAction, OperationAcceptedResponse>
{
    const string CommandKind = "AddCorrectiveAction";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<AddCorrectiveAction> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.ActionId == Uuid.Empty || request.ExpectedFindingRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A corrective action requires its own identity and a non-negative expected finding revision.");
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.ActionId);
        var provenance = FindingContent.Provenance(request.ActionId, CommandKind, snapshot.Stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), (ulong)request.ExpectedFindingRevision,
            FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var failure = FindingRules.ValidateAction(request.ActionId, request.Description, request.OwnerMemberId,
                request.DueOn);
            if (failure is not null)
                return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
            var prepared = owner.PrepareWrite(request.ExpectedFindingRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (owner.HasAdmitted(request.ActionId))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A corrective action identity cannot be reused."));
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, token)
                    .ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(FindingCommands.InactiveOwner());
            var authored = await FindingContent.WriteAsync(content, request.TenantId,
                FindingContent.ActionAuthoredSchema, request with { Description = request.Description.Trim() }, token)
                .ConfigureAwait(false);
            var header = FindingContent.Header(provenance, now);
            intent = await FindingContent.WriteAsync(content, request.TenantId,
                ChildAdmissionIntent.Schema, new ChildAdmissionIntent(header, request.FindingId,
                    (ulong)owner.Generation, AdmittedChildKind.CorrectiveAction, request.ActionId, authored,
                    await RoutedFinding.EmptyProofsAsync(content, request.TenantId, request.ActionId, token)
                        .ConfigureAwait(false), (ulong)request.ExpectedFindingRevision + 1), token).ConfigureAwait(false);
            response = await FindingContent.AcceptedResponseAsync(content, request.TenantId, request.ActionId,
                RegisterFirmStaffHandler.StatusUrlTemplate, now, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.ActionId),
            finding => finding.Acceptance,
            PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef acknowledgment) =>
                finding.AdmitAction(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
