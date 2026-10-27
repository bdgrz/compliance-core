using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
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
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Revises a draft or rejected response into its next draft version at the revision of that response owner. The
///     complete new version is frozen as verified content; there is no lifetime cap on versions, and a delivered version
///     is immutable.
/// </summary>
sealed class ReviseAuditResponseDraftHandler(NativeOperationExecutor operations, AuditorResponseEvidence evidence, IImmutableContentStore content, TimeProvider clock)
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
        var snapshot = new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? version = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(AuditorResponse owner, CancellationToken token)
        {
            var prepared = owner.PrepareRevise(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (AuditorRequestRules.Response(request.Summary, request.EvidenceArtifactIds) is not { } draft)
                return AuditorCommands.Validation<FirstResponseDescriptor>("A response revision needs bounded content and a supported version count.");
            var available = await evidence.RequireAvailableAsync(context, request.TenantId, draft.EvidenceArtifactIds, token)
                .ConfigureAwait(false);
            if (!available.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(available.Error!);
            version = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.VersionSchema,
                new AuditorResponseVersionView((int)owner.BusinessVersion + 1, draft.Summary, draft.EvidenceArtifactIds,
                    "draft", actorMemberId, actorDisplay, now, null, null, null), token).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            PrepareAsync,
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) =>
                auditorResponse.Revise(provenance, version!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
