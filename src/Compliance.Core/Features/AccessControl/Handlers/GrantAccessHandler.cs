using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GrantAccessHandler(IAggregateExecutor executor,
    IAccessGrantProposalValidator validator, IMemberAccessEligibility memberAccess, IAggregateReader reader)
    : IRequestHandler<GrantAccess>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GrantAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.GrantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An access grant requires its tenant and immutable grant identity."));
        var actor = AccessGrantActor.From(context, request.TenantId);
        var original = await ReplayAsync().ConfigureAwait(false);
        if (original is { } retry)
            return retry;
        var proposal = request.Proposal;
        if (proposal is null || proposal.Principal is null)
            return await FailureAsync(await validator.ValidateAsync(request.TenantId, proposal!, ct)
                .ConfigureAwait(false)).ConfigureAwait(false);

        Uuid? membershipEpisodeId = null;
        if (proposal.Principal.Kind == AccessGrantPrincipalKind.Member)
        {
            membershipEpisodeId = await memberAccess.GetMembershipEpisodeIdAsync(request.TenantId,
                proposal.Principal.Id, ct).ConfigureAwait(false);
            if (membershipEpisodeId is null)
                return await FailureAsync(Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The access grant member was not found in this organization."))).ConfigureAwait(false);
        }
        var validation = await validator.ValidateAsync(request.TenantId, proposal, ct).ConfigureAwait(false);
        if (!validation.IsSuccess)
            return await FailureAsync(validation).ConfigureAwait(false);
        if (proposal.Principal.Kind == AccessGrantPrincipalKind.Member)
        {
            var currentEpisodeId = await memberAccess.GetMembershipEpisodeIdAsync(request.TenantId,
                proposal.Principal.Id, ct).ConfigureAwait(false);
            if (currentEpisodeId != membershipEpisodeId)
                return await FailureAsync(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The member's tenant authority changed while the grant was being prepared."))).ConfigureAwait(false);
            if (membershipEpisodeId == Uuid.Empty)
                membershipEpisodeId = null;
        }
        var terms = new AccessGrantTerms(proposal.Principal, proposal.RoleId, proposal.Scope,
            proposal.Source, actor, proposal.EffectiveFrom, proposal.EffectiveUntil);
        try
        {
            var committed = await executor.ExecuteAsync(new AccessGrant(request.TenantId, request.GrantId),
                grant => AggregateOutcome.CommitOnSuccess(grant.Issue(terms, membershipEpisodeId)), context, ct)
                .ConfigureAwait(false);
            return committed.IsSuccess ? committed : await FailureAsync(committed).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException)
        {
            var resolved = await ReplayAsync().ConfigureAwait(false);
            if (resolved is { } accepted)
                return accepted;
            throw;
        }

        async ValueTask<Result?> ReplayAsync()
        {
            var grant = await reader.HydrateAsync(new AccessGrant(request.TenantId, request.GrantId), ct)
                .ConfigureAwait(false);
            return grant.ReplayIssue(request.Proposal, actor);
        }
        async ValueTask<Result> FailureAsync(Result failure) =>
            await ReplayAsync().ConfigureAwait(false) ?? failure;
    }
}
