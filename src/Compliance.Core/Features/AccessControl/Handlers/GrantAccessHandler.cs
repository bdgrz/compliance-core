using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
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
