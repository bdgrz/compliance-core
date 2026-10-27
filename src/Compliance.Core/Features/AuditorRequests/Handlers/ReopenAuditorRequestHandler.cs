using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Reopens a resolved request at its owner revision. The generation advances; all historical responses are retained,
///     and an exact retry of the old resolution returns its old result without closing the new generation.
/// </summary>
public sealed class ReopenAuditorRequestHandler(NativeOperationExecutor operations, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReopenAuditorRequest>
{
    const string CommandKind = "ReopenAuditorRequest";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReopenAuditorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Reopening an auditor request requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRevision < 0)
            return AuditorCommands.Validation("A reopening requires a command id and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? reopen = null;
        FirstResponseDescriptor? response = null;
        AuditorRequestPreparedCut? preparedCut = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                _ = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var scoped = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, prepareCt).ConfigureAwait(false);
                if (!scoped.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(scoped.Error!);
                var prepared = snapshot.PrepareReopen(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var reason = AuditorRequestRules.ReopenReason(request.ReopenReason);
                if (reason is null)
                    return AuditorCommands.Validation<FirstResponseDescriptor>("Reopening a request needs a bounded explanation.");
                reopen = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.ReopenSchema, new AuditorRequestReopenContent(reason, actorMemberId, actorDisplay, now), prepareCt)
                .ConfigureAwait(false);
                (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, prepareCt).ConfigureAwait(false);
                preparedCut = AuditorRequestPreparedCut.Capture(snapshot);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(auditorRequest) != true)
                    return AuditorRequestPreparedCut.Changed();
                return auditorRequest.Reopen(provenance, reopen!, actorMemberId, now, frozen, result!);
            }), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
