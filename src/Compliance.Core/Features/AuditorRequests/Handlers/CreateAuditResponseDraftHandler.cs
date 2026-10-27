using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Admits one response draft on the open request. The complete normalized draft, the request's review policy and
///     the request generation are frozen on the request before the 202 acknowledgment is frozen on the admission, so an
///     exact retry replays the original acknowledgment even after the request changed; the response owner is then
///     materialized by <c>IndependentChildMaterialization</c>. A request that is resolving or resolved admits nothing.
/// </summary>
sealed class CreateAuditResponseDraftHandler(NativeOperationExecutor operations, IDomainEventReader events,
    AuditorResponseEvidence evidence, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CreateAuditResponseDraft, OperationAcceptedResponse>
{
    const string CommandKind = "CreateAuditResponseDraft";
    const string ClosedMessage = "A response can only be drafted for an open request.";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<CreateAuditResponseDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden<OperationAcceptedResponse>(
                "Creating an audit response requires a personal HTTP actor.");
        if (request.ResponseId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRequestRevision < 0)
            return AuditorCommands.Validation<OperationAcceptedResponse>(
                "A response requires its own identity and a non-negative expected request revision.");
        var now = clock.GetUtcNow();
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId);
        var provenance = AuditorContent.Provenance(request.ResponseId, CommandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRequestRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        AuditorRequestPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                _ = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var terms = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, prepareCt).ConfigureAwait(false);
                if (!terms.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(terms.Error!);
                var prepared = snapshot.PrepareWrite(request.ExpectedRequestRevision, ClosedMessage);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var normalized = AuditorRequestRules.Response(request.Summary, request.EvidenceArtifactIds);
                if (normalized is not { } draft)
                    return AuditorCommands.Validation<FirstResponseDescriptor>(
                        "A response draft requires a stable identity and bounded content.");
                var available = await evidence.RequireAvailableAsync(context, request.TenantId, draft.EvidenceArtifactIds, prepareCt)
                .ConfigureAwait(false);
                if (!available.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(available.Error!);
                var authored = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.ResponseDraftSchema,
                new AuditorResponseDraftContent(request.ProgramId, request.RequestId, request.ResponseId, draft.Summary,
                    draft.EvidenceArtifactIds, terms.Value!.RequiresInternalReview, actorMemberId, actorDisplay, now), prepareCt)
                .ConfigureAwait(false);
                intent = await AuditorContent.WriteAsync(content, request.TenantId, ChildAdmissionIntent.Schema,
                new ChildAdmissionIntent(AuditorContent.Header(provenance, now), request.RequestId,
                    (ulong)snapshot.Generation, AdmittedChildKind.AuditorResponse, request.ResponseId, authored,
                    await AuditorContent.EmptyProofsAsync(content, request.TenantId, request.ResponseId, prepareCt)
                        .ConfigureAwait(false), (ulong)request.ExpectedRequestRevision + 1), prepareCt).ConfigureAwait(false);
                response = await AuditorContent.AcceptedResponseAsync(content, request.TenantId, request.ResponseId, now, prepareCt)
                .ConfigureAwait(false);
                preparedCut = AuditorRequestPreparedCut.Capture(snapshot);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
            {
                if (preparedCut?.Matches(auditorRequest) != true)
                    return AuditorRequestPreparedCut.Changed();
                return auditorRequest.AdmitResponse(provenance, intent!, acknowledgment);
            }), context, ct).ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
