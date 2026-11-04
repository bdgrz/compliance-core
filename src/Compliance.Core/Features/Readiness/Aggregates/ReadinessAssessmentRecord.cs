using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Aggregates;

/// <summary>One assessment owns its one evaluation; no program adoption or mutable verdict owner.</summary>
public sealed class ReadinessAssessmentRecord : Aggregate
{
    public const string Area = "readiness-assessment-records";
    readonly Uuid _tenantId;

    public ReadinessAssessmentRecord(Uuid tenantId, Uuid assessmentId)
        : base(assessmentId, new EventStreamAddress(tenantId.ToString(), Area, assessmentId.ToString()))
    {
        _tenantId = tenantId;
        On<ReadinessAssessmentRecordedV2>(Apply);
    }

    public ReadinessAssessmentRecordedV2? Recorded { get; private set; }

    public Result? Replay(RecordReadinessAssessment request, ActorReference actor)
    {
        if (Recorded is not { } recorded)
            return null;
        return request.TenantId == _tenantId && request.AssessmentId == Id &&
            recorded.AuthoredIntentSha256 == AuthoredIntentDigest.Compute(request) &&
            recorded.RequestedBy.Kind == actor.Kind && recorded.RequestedBy.Id == actor.Id
            ? Result.Success : Failure("The assessment identity already records another actor or authored request.");
    }

    public Result Record(RecordReadinessAssessment request, string ruleVersion, DateTimeOffset asOf,
        ImmutableContentRef result, ActorReference actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(actor);
        if (Replay(request, actor) is { } replay)
            return replay;
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || request.TenantId != _tenantId ||
            request.AssessmentId != Id || request.ProgramId == Uuid.Empty ||
            ruleVersion != ReadinessRules.Version || asOf == default || asOf > now || now == default ||
            !ValidContent(result) || string.IsNullOrWhiteSpace(actor.Id) || string.IsNullOrWhiteSpace(actor.Display) ||
            actor.Kind is not ("member" or "system_process") ||
            request.ScheduledDay is { } day &&
                (Id != DailyReadinessAssessment.IdFor(_tenantId, request.ProgramId, day) ||
                 actor.Kind != "system_process" || actor.Id != DailyReadinessAssessment.ProcessId ||
                 day != DateOnly.FromDateTime(asOf.UtcDateTime)))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An assessment requires its exact tenant, program, released rules and valid daily identity."));
        RaiseEvent(new ReadinessAssessmentRecordedV2(_tenantId, request.ProgramId, Id, ruleVersion, asOf,
            result, actor, now, request.ScheduledDay, AuthoredIntentDigest.Compute(request)));
        return Result.Success;
    }

    void Apply(ReadinessAssessmentRecordedV2 fact)
    {
        if (Recorded is not null || fact.TenantId != _tenantId || fact.AssessmentId != Id ||
            fact.ProgramId == Uuid.Empty || string.IsNullOrWhiteSpace(fact.RuleVersion) ||
            fact.AsOf == default || fact.RecordedAt == default || fact.AsOf > fact.RecordedAt ||
            !ValidContent(fact.ResultContent) || fact.AuthoredIntentSha256 is not { Length: 64 } ||
            !fact.AuthoredIntentSha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(fact.RequestedBy.Id) ||
            string.IsNullOrWhiteSpace(fact.RequestedBy.Display) ||
            fact.ScheduledDay is { } day && (Id != DailyReadinessAssessment.IdFor(_tenantId, fact.ProgramId, day) ||
                fact.RequestedBy.Kind != "system_process" || fact.RequestedBy.Id != DailyReadinessAssessment.ProcessId ||
                day != DateOnly.FromDateTime(fact.AsOf.UtcDateTime)))
            throw new InvalidOperationException("The immutable assessment fact is invalid.");
        Recorded = fact;
    }

    static bool ValidContent(ImmutableContentRef content) => content.ContentId != Uuid.Empty && content.Bytes > 0 &&
        content.Sha256 is { Length: 64 } && content.Sha256.All(Uri.IsHexDigit) &&
        content.MediaType == "application/json" && content.Schema == ReadinessAssessmentResult.Schema;
    static Result Failure(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
