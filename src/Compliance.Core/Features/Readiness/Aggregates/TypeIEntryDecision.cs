using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Aggregates;

/// <summary>One decision owns its immutable outcome; current decision is a read model.</summary>
public sealed class TypeIEntryDecision : Aggregate
{
    public const string Area = "type-i-entry-decisions";
    readonly Uuid _tenantId;

    public TypeIEntryDecision(Uuid tenantId, Uuid decisionId)
        : base(decisionId, new EventStreamAddress(tenantId.ToString(), Area, decisionId.ToString()))
    {
        _tenantId = tenantId;
        On<TypeIEntryDecided>(Apply);
    }

    public long Revision => Recorded is null ? 0 : 1;
    public TypeIEntryDecided? Recorded { get; private set; }

    public Result? Replay(DecideTypeIEntryV3 request, ActorReference actor)
    {
        if (Recorded is not { } recorded)
            return null;
        return request.TenantId == _tenantId && request.DecisionId == Id &&
            recorded.AuthoredIntentSha256 == AuthoredIntentDigest.Compute(request) &&
            recorded.DecidedBy.Kind == actor.Kind && recorded.DecidedBy.Id == actor.Id
            ? Result.Success
            : Failure(RequestErrorKind.Conflict, "The decision identity already records another actor or intent.");
    }

    public Result Decide(DecideTypeIEntryV3 request, TypeIEntryAssessmentSnapshot assessment,
        Uuid? replacesDecisionId, ActorReference actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(actor);
        if (Replay(request, actor) is { } replay)
            return replay;
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || request.TenantId != _tenantId ||
            request.DecisionId != Id || request.ProgramId == Uuid.Empty || request.AssessmentId == Uuid.Empty ||
            assessment.TenantId != _tenantId || assessment.ProgramId != request.ProgramId ||
            assessment.AssessmentId != request.AssessmentId || string.IsNullOrWhiteSpace(assessment.RuleVersion) ||
            assessment.AsOf == default || !Hash(assessment.InputFingerprint) ||
            request.Outcome is not ("enter_type_i" or "not_yet") ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000 ||
            replacesDecisionId == Id || replacesDecisionId == Uuid.Empty || now == default ||
            actor.Kind != "member" || !Uuid.TryParse(actor.Id, out var memberId) || memberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(actor.Display))
            return Failure(RequestErrorKind.Validation, "A Type I decision requires exact scope, a complete assessment, an outcome and rationale.");
        if (assessment.Verdict is not ("ready" or "not_ready"))
            return Failure(RequestErrorKind.Conflict, "Only a complete readiness assessment can support a Type I decision.");
        if ((assessment.Verdict == "ready") != (assessment.OpenGaps.Count == 0) ||
            assessment.OpenGaps.Any(static gap => gap.GapId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(gap.RuleId) || string.IsNullOrWhiteSpace(gap.Subject) ||
                string.IsNullOrWhiteSpace(gap.Explanation) || gap.Plan is { } plan && plan.GapId != gap.GapId) ||
            assessment.OpenGaps.Select(static gap => gap.GapId).Distinct().Count() != assessment.OpenGaps.Count)
            return Failure(RequestErrorKind.Validation, "The assessment verdict and copied gap identities must agree.");
        var gaps = assessment.OpenGaps.Select(static gap => gap with { Sources = gap.Sources.ToArray() }).ToArray();
        RaiseEvent(new TypeIEntryDecided(_tenantId, Id, request.ProgramId, request.AssessmentId,
            assessment.RuleVersion, assessment.AsOf, assessment.InputFingerprint, assessment.Verdict,
            request.Outcome, gaps, request.Rationale.Trim(), replacesDecisionId, memberId, actor, now,
            AuthoredIntentDigest.Compute(request)));
        return Result.Success;
    }

    public TypeIEntryDecisionRecord? View() => Recorded is not { } fact ? null : new(fact.TenantId,
        fact.ProgramId, fact.DecisionId, fact.AssessmentId, fact.RuleVersion, fact.AsOf,
        fact.InputFingerprint, fact.Verdict, fact.Outcome, fact.OpenGaps, fact.Rationale,
        fact.ReplacesDecisionId, fact.DeciderMemberId, fact.DecidedBy, fact.DecidedAt);

    void Apply(TypeIEntryDecided fact)
    {
        if (Recorded is not null || fact.TenantId != _tenantId || fact.DecisionId != Id ||
            fact.ProgramId == Uuid.Empty || fact.AssessmentId == Uuid.Empty ||
            fact.Verdict is not ("ready" or "not_ready") || fact.Outcome is not ("enter_type_i" or "not_yet") ||
            (fact.Verdict == "ready") != (fact.OpenGaps.Count == 0) ||
            !Hash(fact.InputFingerprint) || !Hash(fact.AuthoredIntentSha256) ||
            string.IsNullOrWhiteSpace(fact.RuleVersion) || string.IsNullOrWhiteSpace(fact.Rationale) ||
            fact.Rationale != fact.Rationale.Trim() || fact.Rationale.Length > 4000 ||
            fact.DeciderMemberId == Uuid.Empty || fact.DecidedBy.Kind != "member" ||
            fact.DecidedBy.Id != fact.DeciderMemberId.ToString() ||
            string.IsNullOrWhiteSpace(fact.DecidedBy.Display) || fact.DecidedAt == default || fact.AsOf == default ||
            fact.ReplacesDecisionId == Id || fact.ReplacesDecisionId == Uuid.Empty)
            throw new InvalidOperationException("The immutable Type I entry decision fact is invalid.");
        Recorded = fact;
    }

    static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
