using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Cancels an open request with an attributed rationale. Cancellation and fulfilment serialize on the request, and
///     the frozen first response is the request view after the cancellation.
/// </summary>
public sealed class CancelEvidenceRequestHandler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CancelEvidenceRequest, EvidenceRequestView>
{
    public async ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<CancelEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation requires a command id and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new EvidenceRequest(request.TenantId, request.ProgramId,
            request.EvidenceRequestId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "CancelEvidenceRequest", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(EvidenceRequest owner, CancellationToken token)
        {
            var prepared = owner.PrepareDecision(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var invalid = EvidenceRequestRules.ValidateCancel(request.Rationale);
            if (invalid is not null)
                return Result<FirstResponseDescriptor>.Failure(invalid.ToRequestError());
            var view = owner.View()! with
            {
                Revision = owner.Revision + 1,
                Status = EvidenceRequest.Cancelled,
                CancellationRationale = request.Rationale.Trim(),
                CancelledBy = actorReference,
                CancelledAt = now,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                EvidenceRequestRules.ViewSchema, view, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<EvidenceRequest>(binding,
            () => new EvidenceRequest(request.TenantId, request.ProgramId, request.EvidenceRequestId, request.CommandId),
            evidenceRequest => evidenceRequest.Acceptance,
            PrepareAsync,
            (EvidenceRequest evidenceRequest, ImmutableContentRef frozen) => evidenceRequest.Cancel(provenance,
                request.Rationale.Trim(), actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<EvidenceRequestView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
