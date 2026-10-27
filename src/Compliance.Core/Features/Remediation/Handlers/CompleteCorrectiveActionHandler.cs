using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     Records completed corrective work by the action owner or a program manager, on the action's own owner. The
///     expected revision is the revision of the action. The complete evidence list is verified content, an action held
///     by a finding closure refuses the completion until the use settles, and the frozen first response is the finding
///     view after the completion.
/// </summary>
public sealed class CompleteCorrectiveActionHandler(NativeOperationExecutor operations, FindingReader findings,
    OperatingAuthority authority, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CompleteCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<CompleteCorrectiveAction> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A completion requires a command id, an action and a non-negative expected revision."));
        var now = clock.GetUtcNow();
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var snapshot = new CorrectiveAction(request.TenantId, request.ProgramId,
            request.FindingId, request.ActionId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "CompleteCorrectiveAction", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? evidence = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        IReadOnlyList<EvidenceReference> normalized = [];
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(CorrectiveAction owner, CancellationToken token)
        {
            var current = await findings.ReadOneAsync(request.TenantId, request.ProgramId, request.FindingId, now, token)
                .ConfigureAwait(false);
            if (!current.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(current.Error!);
            var view = current.Value!;
            var action = view.CorrectiveActions.FirstOrDefault(item => item.ActionId == request.ActionId);
            if (action?.OwnerMemberId != actor.MemberId && !await authority.ManagesProgramAsync(request.TenantId, actor,
                    request.ProgramId, token).ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only the corrective action owner or a program manager may complete it."));
            if (action is null)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The corrective action was not found."));
            if (!owner.IsMaterialized)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The corrective action is not materialized yet; retry shortly.", true));
            var failure = FindingRules.ValidateCompletion(request.ResolutionNotes, request.Evidence);
            if (failure is not null)
                return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
            var prepared = owner.PrepareComplete(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            normalized = FindingRules.AsUnresolved(request.Evidence);
            evidence = await FindingContent.WriteAsync(content, request.TenantId, FindingContent.EvidenceSchema,
                normalized, token).ConfigureAwait(false);
            var completed = action with
            {
                Status = FindingStates.Completed,
                ResolutionNotes = request.ResolutionNotes.Trim(),
                Evidence = normalized,
                CompletedByMemberId = actor.MemberId,
                CompletedBy = actorReference,
                CompletedAt = now,
                Revision = owner.Revision + 1,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithCompletedAction(view, completed, now), token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CorrectiveAction>(binding,
            () => new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId, request.ActionId,
                request.CommandId),
            action => action.Acceptance,
            PrepareAsync,
            (CorrectiveAction action, ImmutableContentRef frozen) => action.Complete(provenance,
                request.ResolutionNotes.Trim(), evidence!, actor.MemberId, actorReference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
