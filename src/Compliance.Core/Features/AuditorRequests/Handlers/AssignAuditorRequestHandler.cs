using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Assigns an open request to an active client member with a due date at its owner revision. The complete terms after
///     the assignment are frozen as verified content; an exact retry replays the original Unit result.
/// </summary>
public sealed class AssignAuditorRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<AssignAuditorRequest>
{
    const string CommandKind = "AssignAuditorRequest";
    const string ResolvedMessage = "A resolved auditor request must be reopened before reassignment.";

    public async ValueTask<Result> HandleAsync(IRequestContext<AssignAuditorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Assigning an auditor request requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRevision < 0)
            return AuditorCommands.Validation("An assignment requires a command id and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? terms = null;
        FirstResponseDescriptor? response = null;
        AuditorRequestPreparedCut? preparedCut = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                _ = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var current = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, prepareCt).ConfigureAwait(false);
                if (!current.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(current.Error!);
                var prepared = snapshot.PrepareWrite(request.ExpectedRevision, ResolvedMessage);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var assigned = AuditorRequestRules.AssignTerms(current.Value!, request, actorMemberId, actorDisplay, now);
                if (!assigned.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(assigned.Error!);
                if (!await AuditorRequestOwner.IsActiveClientMemberAsync(reader, request.TenantId, request.OwnerMemberId, prepareCt)
                    .ConfigureAwait(false))
                    return Result<FirstResponseDescriptor>.Failure(AuditorRequestOwner.InvalidOwner());
                terms = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.TermsSchema, assigned.Value!, prepareCt).ConfigureAwait(false);
                (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, prepareCt).ConfigureAwait(false);
                preparedCut = AuditorRequestPreparedCut.Capture(snapshot);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(auditorRequest) != true)
                    return AuditorRequestPreparedCut.Changed();
                return auditorRequest.Assign(provenance, terms!, actorMemberId, now, frozen, result!);
            }), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
