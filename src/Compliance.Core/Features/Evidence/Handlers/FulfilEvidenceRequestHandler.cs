using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Handlers;

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
            if (request.ArtifactId == Uuid.Empty)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "Fulfilment requires an evidence artifact."));
            var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId, request.ArtifactId), token)
                .ConfigureAwait(false);
            if (!artifact.IsCreated)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The evidence artifact was not found."));
            if (artifact.State != EvidenceArtifactStates.Available)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Only an available evidence artifact can fulfil a request."));
            var view = owner.View()! with
            {
                Revision = owner.Revision + 1,
                Status = EvidenceRequest.Fulfilled,
                ArtifactId = request.ArtifactId,
                FulfilledBy = actorReference,
                FulfilledAt = now,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                EvidenceRequestRules.ViewSchema, view, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<EvidenceRequest>(binding,
            () => new EvidenceRequest(request.TenantId, request.ProgramId, request.EvidenceRequestId, request.CommandId),
            evidenceRequest => evidenceRequest.Acceptance,
            PrepareAsync,
            (EvidenceRequest evidenceRequest, ImmutableContentRef frozen) => evidenceRequest.Fulfil(provenance,
                request.ArtifactId, actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<EvidenceRequestView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
