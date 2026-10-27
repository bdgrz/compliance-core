using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class ApprovePolicyHandler(PolicyOperations operations, PolicyImpactService impact) : IRequestHandler<ApprovePolicy, PolicyVersionView>
{
    public ValueTask<Result<PolicyVersionView>> HandleAsync(IRequestContext<ApprovePolicy> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result<PolicyVersionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Policy sign-off requires personal HTTP submission.")));
        var request = context.Request;
        SeparationOfDutiesWaiverProof? proof = null;
        return operations.ExecuteAsync<ApprovePolicy, PolicyVersionView>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(ApprovePolicy), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (snapshot.DraftPredecessorVersion is not null)
                {
                    var preview = await impact.PreviewAsync(request.TenantId, request.ProgramId, request.PolicyId,
                        request.ExpectedRevision, token).ConfigureAwait(false);
                    if (!preview.IsSuccess)
                        return Result.Failure(preview.Error!);
                    if (!StringComparer.Ordinal.Equals(preview.Value.Digest, request.ImpactDigest))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The impact preview changed. Reload it before approving."));
                }
                var resolved = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.PolicyId, snapshot.Revision, SeparationOfDutiesActions.Approve, at, token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result.Failure(resolved.Error!);
                proof = resolved.Value;
                return Result.Success;
            },
            (rules, actor, at) => PolicyOperations.Outcome(rules.Approve(request.ProgramId, request.ExpectedRevision, request.CommandId, request.AcceptedReviewDecisionId, request.EffectiveFrom, request.Major, request.Rationale, request.ImpactDigest, actor.Reference, actor.MemberId, at, proof)), () => proof, ct);
    }
}
