using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Risks.Aggregates;

/// <summary>One source event's program-scoped triage, decided once rather than fanned out to every risk.</summary>
public sealed class RiskReassessmentTrigger : Aggregate
{
    public const string Area = "risk-reassessment-triggers";
    public const int MaximumAffectedRisks = 200;
    readonly Uuid _tenantId;

    public RiskReassessmentTrigger(Uuid tenantId, Uuid triggerId)
        : base(triggerId, new EventStreamAddress(tenantId.ToString(), Area, triggerId.ToString()))
    {
        _tenantId = tenantId;
        On<RiskReassessmentTriggerRaised>(Apply);
        On<RiskReassessmentTriggerTriaged>(Apply);
    }

    public Uuid TenantId => _tenantId;
    public long Revision => Triaged is not null ? 2 : Raised is not null ? 1 : 0;
    public RiskReassessmentTriggerRaised? Raised { get; private set; }
    public RiskReassessmentTriggerTriaged? Triaged { get; private set; }

    public static Uuid IdFor(Uuid sourceEventId, Uuid programId) =>
        Uuid.CreateVersion5(sourceEventId, "risk-reassessment:" + programId);

    public CommandFailure? Raise(RaiseRiskReassessmentTrigger request, ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Raised is { } original)
            return request.CommandId == original.CommandId && request.TriggerId == Id &&
                request.TenantId == _tenantId && request.ProgramId == original.ProgramId &&
                digest == original.AuthoredIntentSha256 && SameActor(actor, original.RaisedBy)
                ? null : CommandFailure.StateConflict("The trigger already records another source or authored intent.");
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || request.TenantId != _tenantId || request.TriggerId != Id ||
            request.ProgramId == Uuid.Empty || request.CommandId == Uuid.Empty || !Text(request.Summary) || at == default ||
            request.Kind is not ("manual" or "boundary_change" or "significant_change" or "incident" or "new_material_provider") ||
            request.Kind == "manual" && (request.Source is not null || request.CommandId != Id || actor.Kind != "member") ||
            request.Kind != "manual" && (request.Source is not { } source || source.EventId == Uuid.Empty ||
                source.Source.Realm != _tenantId.ToString() || Id != IdFor(source.EventId, request.ProgramId) || actor.Kind != "system_process"))
            return CommandFailure.InvalidContent("A trigger requires its exact program, recognized source and attributed summary.");
        RaiseEvent(new RiskReassessmentTriggerRaised(_tenantId, request.ProgramId, Id, request.CommandId,
            request.Kind, request.Summary.Trim(), request.Source, digest, actor, at));
        return null;
    }

    public CommandFailure? Triage(TriageRiskReassessmentTrigger request, ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        if (Raised is not { } raised)
            return CommandFailure.MissingRecord("The reassessment trigger was not found.");
        var digest = AuthoredIntentDigest.Compute(request);
        if (Triaged is { } original)
            return request.CommandId == original.CommandId && request.TenantId == _tenantId && request.ProgramId == raised.ProgramId &&
                request.TriggerId == Id && request.ExpectedRevision == original.OriginalExpectedRevision &&
                digest == original.AuthoredIntentSha256 && SameActor(actor, original.TriagedBy)
                ? null : CommandFailure.StateConflict("A triage is final; a missed risk needs a new manual trigger.");
        if (request.TenantId != _tenantId || request.ProgramId != raised.ProgramId || request.TriggerId != Id)
            return CommandFailure.MissingRecord("The reassessment trigger was not found in this program.");
        if (request.ExpectedRevision != Revision)
            return CommandFailure.StateConflict("The trigger changed before triage.");
        if (request.CommandId == Uuid.Empty || request.AffectedRiskIds is not { Count: <= MaximumAffectedRisks } risks ||
            risks.Any(static risk => risk == Uuid.Empty) || risks.Distinct().Count() != risks.Count ||
            (risks.Count == 0 ? !Text(request.ReasonWhenNone) : request.ReasonWhenNone is not null) ||
            actor.Kind != "member" || !Uuid.TryParse(actor.Id, out var member) || member == Uuid.Empty ||
            !Text(actor.Display) || at < raised.RaisedAt)
            return CommandFailure.InvalidContent("Triage names distinct affected risks, or a reason why none are affected.");
        RaiseEvent(new RiskReassessmentTriggerTriaged(_tenantId, raised.ProgramId, Id, request.CommandId, request.ExpectedRevision,
            risks.ToArray(), request.ReasonWhenNone?.Trim(), digest, actor, at));
        return null;
    }

    void Apply(RiskReassessmentTriggerRaised fact)
    {
        if (Raised is not null || fact.TenantId != _tenantId || fact.TriggerId != Id || fact.ProgramId == Uuid.Empty ||
            fact.CommandId == Uuid.Empty || !Text(fact.Summary) || fact.Summary != fact.Summary.Trim() ||
            fact.AuthoredIntentSha256.Length != 64 || fact.RaisedAt == default ||
            fact.Kind == "manual" && (fact.Source is not null || fact.CommandId != Id || fact.RaisedBy.Kind != "member") ||
            fact.Kind != "manual" && (fact.Source is not { } source || source.Source.Realm != _tenantId.ToString() ||
                Id != IdFor(source.EventId, fact.ProgramId) || fact.RaisedBy.Kind != "system_process"))
            throw new InvalidOperationException("The reassessment trigger source is invalid.");
        Raised = fact;
    }

    void Apply(RiskReassessmentTriggerTriaged fact)
    {
        if (Raised is not { } raised || Triaged is not null || fact.TenantId != _tenantId ||
            fact.ProgramId != raised.ProgramId || fact.TriggerId != Id || fact.OriginalExpectedRevision != 1 ||
            fact.CommandId == Uuid.Empty || fact.TriagedBy.Kind != "member" || fact.TriagedAt < raised.RaisedAt ||
            fact.AffectedRiskIds is not { Count: <= MaximumAffectedRisks } risks || risks.Any(static risk => risk == Uuid.Empty) ||
            risks.Distinct().Count() != risks.Count || (risks.Count == 0 ? !Text(fact.ReasonWhenNone) : fact.ReasonWhenNone is not null))
            throw new InvalidOperationException("The reassessment triage is invalid or not terminal.");
        Triaged = fact;
    }

    static bool SameActor(ActorReference left, ActorReference right) => left.Kind == right.Kind && left.Id == right.Id;
    static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 2000;
}
