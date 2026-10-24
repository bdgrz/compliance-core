using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Revises a draft or rejected response into its next draft version at the revision of that response owner. The
///     complete new version is frozen as verified content; there is no lifetime cap on versions, and a delivered version
///     is immutable.
/// </summary>
sealed class ReviseAuditResponseDraftHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AuditorResponseEvidence evidence, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviseAuditResponseDraft>
{
    const string CommandKind = "ReviseAuditResponseDraft";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditResponseDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Revising an audit response requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ResponseId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AuditorCommands.Validation(
                "A response revision requires a command id, a response and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? version = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareRevise(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            if (AuditorRequestRules.Response(request.Summary, request.EvidenceArtifactIds) is not { } draft)
                return AuditorCommands.Validation("A response revision needs bounded content and a supported version count.");
            var available = await evidence.RequireAvailableAsync(context, request.TenantId, draft.EvidenceArtifactIds, ct)
                .ConfigureAwait(false);
            if (!available.IsSuccess)
                return available;
            version = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.VersionSchema,
                new AuditorResponseVersionView((int)snapshot.BusinessVersion + 1, draft.Summary, draft.EvidenceArtifactIds,
                    "draft", actorMemberId, actorDisplay, now, null, null, null), ct).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            auditorResponse => AuditorContent.Prepared(auditorResponse.PrepareRevise(request.ExpectedRevision), response),
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) =>
                auditorResponse.Revise(provenance, version!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
