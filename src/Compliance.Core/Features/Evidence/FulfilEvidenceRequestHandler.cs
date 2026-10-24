using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Fulfils an open request with a captured artifact. Only the request owner or a program manager may fulfil, and an
///     available artifact may answer a request. Fulfilment and cancellation serialize on the request: of two racing
///     decisions exactly one is accepted. The frozen first response is the request view after the fulfilment.
/// </summary>
public sealed class FulfilEvidenceRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    OperatingAuthority authority, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<FulfilEvidenceRequest, EvidenceRequestView>
{
    public async ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<FulfilEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A fulfilment requires a command id and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new EvidenceRequest(request.TenantId, request.ProgramId,
            request.EvidenceRequestId, request.CommandId), ct).ConfigureAwait(false);
        if (snapshot.Opened is not { } opened)
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence request was not found."));
        if (opened.OwnerMemberId != actor.MemberId &&
            !await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the request owner or a program manager may fulfil this request."));
        var provenance = FindingContent.Provenance(request.CommandId, "FulfilEvidenceRequest", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareDecision(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<EvidenceRequestView>.Failure(prepared.Error!);
            if (request.ArtifactId == Uuid.Empty)
                return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                    "Fulfilment requires an evidence artifact."));
            var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId, request.ArtifactId), ct)
                .ConfigureAwait(false);
            if (!artifact.IsCreated)
                return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The evidence artifact was not found."));
            if (artifact.State != EvidenceArtifactStates.Available)
                return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Only an available evidence artifact can fulfil a request."));
            var view = snapshot.View()! with
            {
                Revision = snapshot.Revision + 1,
                Status = EvidenceRequest.Fulfilled,
                ArtifactId = request.ArtifactId,
                FulfilledBy = actorReference,
                FulfilledAt = now,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                EvidenceRequestRules.ViewSchema, view, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<EvidenceRequest>(binding,
            () => new EvidenceRequest(request.TenantId, request.ProgramId, request.EvidenceRequestId, request.CommandId),
            evidenceRequest => evidenceRequest.Acceptance,
            evidenceRequest => FindingCommands.Prepared(evidenceRequest.PrepareDecision(request.ExpectedRevision),
                response),
            (EvidenceRequest evidenceRequest, ImmutableContentRef frozen) => evidenceRequest.Fulfil(provenance,
                request.ArtifactId, actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<EvidenceRequestView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
