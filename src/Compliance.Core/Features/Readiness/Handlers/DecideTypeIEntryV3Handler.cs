using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Current Org Admin authority is checked before first admission or accepted replay.</summary>
public sealed class DecideTypeIEntryV3Handler(IAggregateExecutor executor, IAggregateReader reader,
    ReadinessReader assessments, ReadinessAssessmentRecordReader records, TypeIEntryDecisionReader decisions, IAccessGrantRolePermissionAuthorizer roles,
    ClientManagementIndependenceGuard independence, ProgramApplicationAdmission programs, TimeProvider clock)
    : IRequestHandler<DecideTypeIEntryV3, TypeIEntryDecisionRecord>
{
    public async ValueTask<Result<TypeIEntryDecisionRecord>> HandleAsync(
        IRequestContext<DecideTypeIEntryV3> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Failure(RequestErrorKind.Forbidden, "A current Org Admin must decide Type I entry.");
        var memberId = RbacIds.Member(request.TenantId, userId);
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, memberId), ct).ConfigureAwait(false);
        if (member.UserId != userId || member.Affiliation != "client_personnel" ||
            !member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime)) ||
            !await independence.CanAuthorAsync(request.TenantId, userId, ct).ConfigureAwait(false) ||
            !await roles.IsAllowedAsRoleAsync(request.TenantId, userId, memberId, request.ProgramId,
                BuiltInRbac.TenantAdministrationRoleId(request.TenantId), "program.manage", ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden, "A current Org Admin must decide Type I entry.");
        var active = await programs.CheckAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (!active.IsSuccess)
            return Result<TypeIEntryDecisionRecord>.Failure(active.Error);
        var actor = ActorReference.ForMember(memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        var owner = await reader.HydrateAsync(new TypeIEntryDecision(request.TenantId, request.DecisionId), ct).ConfigureAwait(false);
        if (owner.Replay(request, actor) is { } replay)
            return replay.IsSuccess ? Result<TypeIEntryDecisionRecord>.Success(owner.View()!)
                : Result<TypeIEntryDecisionRecord>.Failure(replay.Error);
        // Exact historical assessment content is authoritative; no latest-adoption or runner-independence gate.
        var target = await records.ReadAsync(request.TenantId, request.ProgramId, request.AssessmentId, ct).ConfigureAwait(false);
        TypeIEntryAssessmentSnapshot snapshot;
        if (target is not null)
        {
            if (!target.Completeness.IsComplete)
                return Failure(RequestErrorKind.Conflict, "Only a complete readiness assessment can support a Type I decision.");
            var gaps = await assessments.WithPlansAsync(request.TenantId, request.ProgramId, target.Gaps, ct).ConfigureAwait(false);
            snapshot = new TypeIEntryAssessmentSnapshot(request.TenantId, request.ProgramId, request.AssessmentId,
                target.RuleVersion, target.AsOf, target.InputFingerprint, target.Completeness.Verdict, gaps);
        }
        else
        {
            var assessment = await assessments.ReadContentAsync(request.TenantId, request.ProgramId,
                request.AssessmentId, ct).ConfigureAwait(false);
            if (assessment is null)
                return Failure(RequestErrorKind.NotFound, "The readiness assessment was not found in this program.");
            var completeness = ReadinessCompletenessRules.Evaluate(assessment.Inputs, assessment.Gaps);
            if (!completeness.IsComplete)
                return Failure(RequestErrorKind.Conflict, "Only a complete readiness assessment can support a Type I decision.");
            var gaps = await assessments.WithPlansAsync(request.TenantId, request.ProgramId, assessment.Gaps, ct).ConfigureAwait(false);
            snapshot = new TypeIEntryAssessmentSnapshot(request.TenantId, request.ProgramId, request.AssessmentId,
                assessment.Fact.RuleVersion, assessment.Fact.AsOf, assessment.Fact.InputFingerprint, completeness.Verdict, gaps);
        }
        var previous = await decisions.CurrentAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new TypeIEntryDecision(request.TenantId, request.DecisionId), fresh =>
        {
            var result = fresh.Decide(request, snapshot, previous?.DecisionId, actor, now);
            return result.IsSuccess
                ? AggregateOutcome.Commit(Result<TypeIEntryDecisionRecord>.Success(fresh.View()!))
                : AggregateOutcome.Discard(Result<TypeIEntryDecisionRecord>.Failure(result.Error));
        }, context, ct).ConfigureAwait(false);
    }

    static Result<TypeIEntryDecisionRecord> Failure(RequestErrorKind kind, string message) =>
        Result<TypeIEntryDecisionRecord>.Failure(new RequestError(kind, message));
}
