using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

public sealed class RecordReadinessAssessmentHandler(IAggregateExecutor executor, IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions, ProgramApplicationAdmission programs,
    ReadinessAssessmentCalculator calculator, ReadinessAssessmentRecordReader assessments,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordReadinessAssessment, ReadinessAssessmentResult>
{
    public async ValueTask<Result<ReadinessAssessmentResult>> HandleAsync(
        IRequestContext<RecordReadinessAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        ActorReference actor;
        if (RequestActor.IsSystem(context.Actor))
        {
            var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
            if (context.Invocation is not DirectInvocation || subjects.Length != 1 ||
                subjects[0].Issuer != "bdgrz.system" || subjects[0].Value != DailyReadinessAssessment.ProcessId ||
                request.ScheduledDay is not { } day || day != DateOnly.FromDateTime(now.UtcDateTime))
                return Failure(RequestErrorKind.Forbidden, "Only the named daily calculator may record scheduled assessments.");
            actor = ActorReference.ForSystemProcess(DailyReadinessAssessment.ProcessId, "Daily readiness assessment");
        }
        else
        {
            if (request.ScheduledDay is not null || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
                return Failure(RequestErrorKind.Forbidden, "An authorized current member must request an assessment.");
            var memberId = RbacIds.Member(request.TenantId, userId);
            var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, memberId), ct).ConfigureAwait(false);
            if (member.UserId != userId || member.Affiliation != "client_personnel" ||
                !member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime)) ||
                !await permissions.IsAllowedAsync(request.TenantId, userId, memberId, request.ProgramId,
                    "tenant.access", ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Forbidden, "An authorized current member must request an assessment.");
            actor = ActorReference.ForMember(memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        }
        var active = await programs.CheckAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (!active.IsSuccess)
            return Result<ReadinessAssessmentResult>.Failure(active.Error);
        var owner = await reader.HydrateAsync(new ReadinessAssessmentRecord(request.TenantId, request.AssessmentId), ct).ConfigureAwait(false);
        if (owner.Replay(request, actor) is { } replay)
            return replay.IsSuccess
                ? Result<ReadinessAssessmentResult>.Success((await assessments.ReadAsync(request.TenantId, request.ProgramId,
                    request.AssessmentId, ct).ConfigureAwait(false))!)
                : Result<ReadinessAssessmentResult>.Failure(replay.Error);
        var result = await calculator.CalculateAsync(request, actor, now, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        var frozen = await content.WriteJsonAsync(request.TenantId.ToString(), Uuid.CreateVersion4(),
            ReadinessAssessmentResult.Schema, result.Value, ct).ConfigureAwait(false);
        var appended = await executor.ExecuteAsync(new ReadinessAssessmentRecord(request.TenantId, request.AssessmentId), fresh =>
        {
            var replay = fresh.Replay(request, actor);
            if (replay is { } prior)
                return AggregateOutcome.Discard(prior.IsSuccess
                    ? Result<ReadinessAssessmentRecordedV2>.Success(fresh.Recorded!)
                    : Result<ReadinessAssessmentRecordedV2>.Failure(prior.Error));
            var recorded = fresh.Record(request, result.Value.RuleVersion, result.Value.AsOf, frozen, actor, now);
            return recorded.IsSuccess
                ? AggregateOutcome.Commit(Result<ReadinessAssessmentRecordedV2>.Success(fresh.Recorded!))
                : AggregateOutcome.Discard(Result<ReadinessAssessmentRecordedV2>.Failure(recorded.Error));
        }, context, ct).ConfigureAwait(false);
        if (!appended.IsSuccess)
            return Result<ReadinessAssessmentResult>.Failure(appended.Error);
        return Result<ReadinessAssessmentResult>.Success(await content.ReadJsonAsync<ReadinessAssessmentResult>(
            request.TenantId.ToString(), appended.Value.ResultContent, ct).ConfigureAwait(false));
    }

    static Result<ReadinessAssessmentResult> Failure(RequestErrorKind kind, string message) =>
        Result<ReadinessAssessmentResult>.Failure(new RequestError(kind, message));
}
