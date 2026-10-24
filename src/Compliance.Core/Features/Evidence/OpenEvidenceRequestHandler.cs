using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Opens an evidence request for an active client member, optionally tied to a control in the program. The request
///     id is the logical operation: an exact retry returns the frozen original request even after later changes, and a
///     different intent under the same id conflicts.
/// </summary>
public sealed class OpenEvidenceRequestHandler(NativeOperationExecutor operations, IAggregateReader reader, ControlReader controlSources,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<OpenEvidenceRequest, EvidenceRequestView>
{
    public ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<OpenEvidenceRequest> context,
        CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<EvidenceRequestView>> HandleCoreAsync(IRequestContext<OpenEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.EvidenceRequestId == Uuid.Empty)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An evidence request requires its request id."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new EvidenceRequest(request.TenantId, request.ProgramId,
            request.EvidenceRequestId, request.EvidenceRequestId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.EvidenceRequestId, "OpenEvidenceRequest", snapshot.Stream,
            actorReference, null, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        EvidenceRequestTerms? terms = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareOpen();
            if (!prepared.IsSuccess)
                return Result<EvidenceRequestView>.Failure(prepared.Error!);
            var invalid = EvidenceRequestRules.ValidateOpen(request.Title, request.Instructions, request.OwnerMemberId,
                request.ControlId, request.DueOn, now);
            if (invalid is not null)
                return Result<EvidenceRequestView>.Failure(invalid.ToRequestError());
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, ct)
                    .ConfigureAwait(false))
                return Result<EvidenceRequestView>.Failure(FindingCommands.InactiveOwner());
            if (request.ControlId is { } controlId)
            {
                var control = await controlSources.ReadAsync(request.TenantId, request.ProgramId, controlId, ct)
                    .ConfigureAwait(false);
                if (!control.IsCreated || control.ProgramId != request.ProgramId)
                    return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                        "The control was not found in this program."));
            }

            terms = new EvidenceRequestTerms(request.Title.Trim(), request.Instructions.Trim(), request.OwnerMemberId,
                request.DueOn, request.ControlId, actorReference, now);
            var view = new EvidenceRequestView(request.TenantId, request.ProgramId, request.EvidenceRequestId, 1,
                terms.Title, terms.Instructions, terms.OwnerMemberId, terms.DueOn, terms.ControlId,
                EvidenceRequest.Open, actorReference, now, null, null, null, null, null, null);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                EvidenceRequestRules.ViewSchema, view, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<EvidenceRequest>(binding,
            () => new EvidenceRequest(request.TenantId, request.ProgramId, request.EvidenceRequestId,
                request.EvidenceRequestId),
            evidenceRequest => evidenceRequest.Acceptance,
            evidenceRequest => FindingCommands.Prepared(evidenceRequest.PrepareOpen(), response),
            (EvidenceRequest evidenceRequest, ImmutableContentRef frozen) =>
                evidenceRequest.OpenRequest(provenance, terms!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<EvidenceRequestView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
