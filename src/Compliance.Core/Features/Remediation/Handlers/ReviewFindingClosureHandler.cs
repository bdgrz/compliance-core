using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.History;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Personally returns or approves the exact current proposal; approved closure still requires final held-source proofs.</summary>
public sealed class ReviewFindingClosureHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IDomainEventReader events, FindingReader findings, IPermissionAuthorizer permissions,
    ISeparationOfDutiesWaiverProofResolver waivers, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviewFindingClosure, OperationAcceptedResponse>
{
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<ReviewFindingClosure> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Finding closure review requires a personal HTTP invocation.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.FindingId == Uuid.Empty || request.ClosureId == Uuid.Empty || request.ExpectedRevision < 0 ||
            request.ProposalRevision < 1 || request.SeparationOfDutiesWaiverId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A closure review requires the exact finding and proposal revision.");
        var now = clock.GetUtcNow();
        var acting = await FindingActors.RequireCurrentAsync(context.Actor, request.TenantId, reader, now, ct).ConfigureAwait(false);
        if (!acting.IsSuccess)
            return Failure(acting.Error!.Kind, acting.Error.Message);
        var actor = acting.Value;
        if (!await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
                FindingClosurePermissions.Review, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden, "Only a Compliance Lead may review a finding closure proposal.");
        var reviewedBy = ActorReference.ForMember(actor.MemberId, actor.Display);
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, nameof(ReviewFindingClosure), snapshot.Stream,
            reviewedBy, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        SeparationOfDutiesWaiverProof? waiver = null;
        ImmutableContentRef? intent = null;
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            if (!await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
                    FindingClosurePermissions.Review, token).ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only a Compliance Lead may review a finding closure proposal."));
            if (request.SeparationOfDutiesWaiverId is { } waiverId)
            {
                var scope = new SeparationOfDutiesWaiverProofScope(request.TenantId, waiverId, actor.MemberId, actor.MemberId,
                    request.Approve ? SeparationOfDutiesActions.Approve : SeparationOfDutiesActions.Review,
                    SeparationOfDutiesRecordTypes.Finding, request.FindingId, request.ClosureId, request.ProposalRevision);
                var resolved = await waivers.ResolveAsync(scope, now, token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                        "The waiver is not active for this reviewer and exact proposal review action."));
                waiver = resolved.Value;
            }
            var prepared = owner.PrepareReview(provenance, request.ClosureId, request.ProposalRevision,
                request.Approve, request.Comments, waiver);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (request.Approve)
            {
                var verified = await FindingClosureProposalSources.VerifyEvidenceAsync(reader, events, request.TenantId,
                    owner.ClosureProposal!.Terms, token).ConfigureAwait(false);
                if (!verified.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(verified.Error!);
                intent = await FreezeApprovalAsync(owner, token).ConfigureAwait(false);
            }
            else
            {
                var current = await findings.ComposeAsync(owner, now, token).ConfigureAwait(false);
                var after = FindingViews.WithReturnedProposal(current!, request.Comments!.Trim(), owner.Revision + 1, reviewedBy, now);
                result = (await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema, after, token)
                    .ConfigureAwait(false)).Result;
            }
            return Result<FirstResponseDescriptor>.Success(await FindingContent.AcceptedResponseAsync(content, request.TenantId,
                request.CommandId, RegisterFirmStaffHandler.StatusUrlTemplate, now, token).ConfigureAwait(false));
        }

        async Task<ImmutableContentRef> FreezeApprovalAsync(FindingAggregate owner, CancellationToken token)
        {
            var proposal = owner.ClosureProposal!;
            var history = await FindingHistory.ReadAsync(events, owner, token).ConfigureAwait(false);
            var children = await FindingClosureProposalSources.ReadActionsAsync(content, owner, history, token).ConfigureAwait(false);
            var review = new FindingClosureReviewBinding(request.ClosureId, request.ProposalRevision, actor.MemberId, reviewedBy, now, waiver);
            var decision = new ClosureDecision(request.CommandId, request.ProgramId, proposal.Terms.VerificationRationale,
                FindingClosureProposalSources.References(proposal.Terms), proposal.Terms.Rationale, actor.MemberId, reviewedBy, now,
                request.SeparationOfDutiesWaiverId, NativeFactReference.Of(owner.Stream, proposal), review);
            var decisionContent = await FindingContent.WriteAsync(content, request.TenantId, FindingContent.DecisionSchema, decision, token)
                .ConfigureAwait(false);
            var uses = children.Select(child => new SourceUseRequirement(
                new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId, child.ChildId).Stream,
                child.Admission, 0, SourceUsePurpose.FindingClosure,
                history.Admitted.First(item => item.Operation.LogicalId == child.ChildId).Intent)).ToArray();
            var useManifest = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, request.TenantId.ToString(),
                Uuid.CreateVersion4(), SourceUseRequirement.Schema, uses, SelectionOrder.CanonicalKey, token).ConfigureAwait(false);
            var waiverContent = waiver is null ? null : await FindingContent.WriteAsync(content, request.TenantId,
                "separation-of-duties-waiver-proof.v1", waiver, token).ConfigureAwait(false);
            return await FindingContent.WriteAsync(content, request.TenantId, ClosureIntent.Schema,
                new ClosureIntent(FindingContent.Header(provenance, now), ClosureParentKind.Finding, request.FindingId,
                    (ulong)owner.Generation, proposal.ActionsReliedOn, decisionContent, useManifest, waiverContent), token).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            owner => owner.Acceptance, PrepareAsync,
            (FindingAggregate owner, ImmutableContentRef first) => owner.ReviewClosure(provenance, request.ClosureId,
                request.ProposalRevision, request.Approve, request.Comments, waiver, intent, first, result)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
