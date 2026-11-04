using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.History;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Proposes resolution over the finding's complete resolved action set and exact captured artifact versions.</summary>
public sealed class ProposeFindingClosureHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IDomainEventReader events, FindingReader findings, IPermissionAuthorizer permissions,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ProposeFindingClosure, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ProposeFindingClosure> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.FindingId == Uuid.Empty || request.ClosureId == Uuid.Empty || request.ExpectedRevision < 0 ||
            request.PreviousProposalRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A closure proposal requires its finding, closure identity and non-negative predecessor revisions."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var proposedBy = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, nameof(ProposeFindingClosure), snapshot.Stream,
            proposedBy, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        FindingClosureTerms? terms = null;
        ImmutableSelectionManifest? actions = null;
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var prepared = owner.PrepareProposal(request.ExpectedRevision, request.ClosureId, request.PreviousProposalRevision, request.Terms);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (owner.OwnerMemberId != actor.MemberId && !await permissions.IsAllowedAsync(request.TenantId, actor.UserId,
                    actor.MemberId, FindingClosurePermissions.Propose, token).ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only the finding owner or a Compliance Lead may propose closure."));
            terms = FindingClosureProposalSources.Freeze(request.Terms);
            var verified = await FindingClosureProposalSources.VerifyEvidenceAsync(reader, events, request.TenantId, terms, token)
                .ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(verified.Error!);
            var history = await FindingHistory.ReadAsync(events, owner, token).ConfigureAwait(false);
            actions = await FindingClosureProposalSources.WriteActionsAsync(content, owner, history, token).ConfigureAwait(false);
            var proposal = FindingClosureProposalSources.View(request.ClosureId, request.PreviousProposalRevision + 1,
                terms, history, proposedBy, now);
            var current = await findings.ComposeAsync(owner, now, token).ConfigureAwait(false);
            var after = FindingViews.WithProposal(current!, proposal, owner.Revision + 1, now);
            var frozen = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema, after, token)
                .ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            owner => owner.Acceptance, PrepareAsync,
            (FindingAggregate owner, ImmutableContentRef first) => owner.ProposeClosure(provenance, request.ClosureId,
                request.PreviousProposalRevision, terms!, actions!, first, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
