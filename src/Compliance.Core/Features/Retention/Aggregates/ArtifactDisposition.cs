using System.Collections.ObjectModel;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
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
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.Retention.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Retention.Aggregates;

/// <summary>One approved disposal intent and its durable, retryable storage execution history.</summary>
public sealed class ArtifactDisposition : Aggregate
{
    const int MaximumAttempts = 16;
    const int MaximumRationaleLength = 2000;
    const int MaximumFailureCodeLength = 128;
    static readonly HashSet<string> RetentionPolicies = new(StringComparer.Ordinal)
    {
        ArtifactRetentionRules.Policy
    };
    static readonly HashSet<string> HandlingClasses = new(StringComparer.Ordinal)
    {
        "public", "internal", "confidential", "restricted"
    };

    readonly Uuid _tenantId;
    ArtifactDispositionView? _current;

    public long Revision => _current?.Revision ?? 0;

    public ArtifactDispositionView? Current => _current;

    public ArtifactDisposition(Uuid tenantId, Uuid dispositionId)
        : base(dispositionId, new EventStreamAddress(tenantId.ToString(), "artifact-dispositions",
            dispositionId.ToString()))
    {
        _tenantId = tenantId;
        On<ArtifactDispositionApproved>(Apply);
        On<ArtifactDispositionExecutionRequested>(Apply);
        On<ArtifactDispositionResultRecorded>(Apply);
    }

    public Result Approve(ArtifactDispositionEligibilitySnapshot eligibility, long expectedRevision,
        string rationale, ActorReference approvedBy, DateTimeOffset approvedAt)
    {
        if (eligibility?.Source?.TenantId != _tenantId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The artifact disposition source was not found in this tenant."));
        if (ValidateEligibility(eligibility) is { } eligibilityError)
            return Result.Failure(eligibilityError);
        if (!ValidMember(approvedBy) || approvedAt == default ||
            !Bounded(rationale, 1, MaximumRationaleLength))
            return Invalid("Disposition approval requires an Org Admin actor and a bounded rationale.");
        rationale = rationale.Trim();
        if (_current is { } existing)
            return SameEligibility(existing, eligibility) && existing.Rationale == rationale &&
                   existing.ApprovedBy == approvedBy && existing.ApprovedAt == approvedAt &&
                   expectedRevision <= Revision
                ? Result.Success
                : Conflict("A disposition identity cannot be reused with changed source or approval intent.");
        if (expectedRevision != 0)
            return StaleRevision();

        var state = FromEligibility(eligibility, Id, rationale, approvedBy, approvedAt);
        RaiseEvent(new ArtifactDispositionApproved(_tenantId, Id, 1, MemberId(approvedBy), state,
            approvedAt));
        return Result.Success;
    }

    public Result RequestExecution(long expectedRevision,
        ArtifactDispositionEligibilitySnapshot eligibility, ActorReference requestedBy,
        DateTimeOffset requestedAt)
    {
        if (_current is not { } current)
            return Missing();
        if (eligibility?.Source?.TenantId != _tenantId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The artifact disposition source was not found in this tenant."));
        if (!ValidActor(requestedBy) || requestedAt == default)
            return Invalid("Disposition execution requires an attributed member or system process.");
        if (current.Status == "executing" && current.Attempts.Count > 0 &&
            current.Attempts[^1] is { Status: "requested" } retry &&
            retry.RequestedBy == requestedBy && retry.RequestedAt == requestedAt &&
            SameEligibility(current, eligibility))
            return Result.Success;
        if (expectedRevision < Revision && current.Attempts.Any(attempt =>
                attempt.RequestedBy == requestedBy && attempt.RequestedAt == requestedAt &&
                attempt.SourcePosition == eligibility.Position))
            return Result.Success;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status is not ("approved" or "failed"))
            return Conflict("Only an approved or failed disposition may be executed or retried.");
        if (ValidateEligibility(eligibility) is { } eligibilityError)
            return Result.Failure(eligibilityError);
        if (!SameEligibility(current, eligibility))
            return Conflict("A hold, reliance, retention, or artifact change blocks disposition execution.");
        if (current.Attempts.Count >= MaximumAttempts)
            return Conflict("The disposition reached its execution-attempt limit.");

        var attempt = new ArtifactDispositionAttemptView(current.Attempts.Count + 1, "requested",
            requestedBy, requestedAt, eligibility.Position, null, null, null, null);
        var next = current with
        {
            Status = "executing",
            Revision = Revision + 1,
            SourcePosition = eligibility.Position,
            ActiveHoldIds = FreezeIds(eligibility.ActiveHoldIds),
            HistoricalRelianceIds = FreezeIds(eligibility.HistoricalRelianceIds),
            ApprovedAsOf = eligibility.AsOf,
            Attempts = Append(current.Attempts, attempt),
            UpdatedAt = requestedAt,
        };
        RaiseEvent(new ArtifactDispositionExecutionRequested(_tenantId, Id, next.Revision,
            MemberId(requestedBy), next, requestedAt));
        return Result.Success;
    }

    public Result RecordExecutionResult(long expectedRevision, string outcome, string? failureCode,
        ActorReference completedBy, DateTimeOffset completedAt)
    {
        if (_current is not { } current)
            return Missing();
        if (!ValidActor(completedBy) || completedAt == default)
            return Invalid("A disposition result requires an attributed executor and completion time.");
        outcome = (outcome ?? string.Empty).Trim().ToLowerInvariant();
        failureCode = string.IsNullOrWhiteSpace(failureCode) ? null : failureCode.Trim();
        for (var index = 0; index < current.Attempts.Count; index++)
        {
            if (current.Attempts[index] is { Status: "failed" or "disposed" } prior &&
                prior.Outcome == outcome && prior.FailureCode == failureCode &&
                prior.CompletedBy == completedBy && prior.CompletedAt == completedAt &&
                (index == current.Attempts.Count - 1 ? expectedRevision <= Revision : expectedRevision < Revision))
                return Result.Success;
        }
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "executing" || current.Attempts.Count == 0 || current.Attempts[^1] is not
            { Status: "requested" } attempt)
            return Conflict("A disposition result requires an outstanding execution attempt.");
        if (outcome is not ("failed" or "disposed") ||
            outcome == "failed" && !Bounded(failureCode, 1, MaximumFailureCodeLength) ||
            outcome == "disposed" && failureCode is not null)
            return Invalid("A disposition result must be failed with a code or successfully disposed.");

        var completed = attempt with
        {
            Status = outcome,
            Outcome = outcome,
            FailureCode = failureCode,
            CompletedBy = completedBy,
            CompletedAt = completedAt,
        };
        var tombstone = outcome == "disposed"
            ? new ArtifactDispositionTombstoneView(current.ArtifactId, current.Source.ContentSha256,
                current.ContentLength, current.HandlingClass, current.RetentionPolicy,
                current.PeriodStart, current.PeriodEnd, current.RetainsThrough, current.Rationale,
                current.ApprovedBy, current.ApprovedAt, completedBy, completedAt)
            : null;
        var next = current with
        {
            Status = outcome,
            Revision = Revision + 1,
            Attempts = ReplaceLast(current.Attempts, completed),
            Tombstone = tombstone,
            UpdatedAt = completedAt,
        };
        RaiseEvent(new ArtifactDispositionResultRecorded(_tenantId, Id, next.Revision,
            MemberId(completedBy), next, completedAt));
        return Result.Success;
    }

    void Apply(ArtifactDispositionApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "approved");

    void Apply(ArtifactDispositionExecutionRequested ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "executing");

    void Apply(ArtifactDispositionResultRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt,
        ev.State?.Status ?? string.Empty);

    void ApplyState(Uuid tenantId, Uuid dispositionId, long revision, Uuid? actorMemberId,
        ArtifactDispositionView state, DateTimeOffset occurredAt, string transition)
    {
        if (tenantId != _tenantId || dispositionId != Id || state is null ||
            state.TenantId != _tenantId || state.DispositionId != Id || state.Revision != revision ||
            state.Status != transition || state.UpdatedAt != occurredAt ||
            actorMemberId != TransitionActor(state, transition) || ValidateState(state) is not null ||
            transition == "approved" && (_current is not null || revision != 1) ||
            transition == "executing" && (_current is null || revision != Revision + 1 ||
                state.Attempts.Count != _current.Attempts.Count + 1 ||
                !SameApproval(_current, state) || state.Attempts[^1].Status != "requested") ||
            transition is "failed" or "disposed" && (_current is null || revision != Revision + 1 ||
                _current.Status != "executing" || state.Attempts.Count != _current.Attempts.Count ||
                !SameApproval(_current, state) ||
                state.Attempts[^1].Status != transition))
            throw new InvalidOperationException("The artifact disposition event is invalid.");
        _current = FreezeView(state);
    }

    static RequestError? ValidateEligibility(ArtifactDispositionEligibilitySnapshot eligibility)
    {
        if (eligibility is null || eligibility.Source is null || eligibility.Source.TenantId == Uuid.Empty ||
            eligibility.Source.SourceId == Uuid.Empty ||
            eligibility.Source.SourceKind is not ("evidence_artifact" or "application_import") ||
            !ValidDigest(eligibility.Source.ContentSha256) || eligibility.Position == 0 ||
            eligibility.ContentLength <= 0 || eligibility.HandlingPolicyRevision <= 0 ||
            eligibility.ArtifactState is not ("available" or "quarantined") ||
            eligibility.ArtifactState == "quarantined" && eligibility.StateReason != "malware" ||
            !HandlingClasses.Contains(eligibility.HandlingClass) ||
            eligibility.RetentionBasisRecorded is false || !RetentionPolicies.Contains(eligibility.RetentionPolicy) ||
            eligibility.PeriodStart == default || eligibility.PeriodEnd == default ||
            eligibility.PeriodStart > eligibility.PeriodEnd || eligibility.PeriodEnd.Year > 9992 ||
            eligibility.RetainsThrough != eligibility.PeriodEnd.AddYears(7) ||
            eligibility.AsOf <= eligibility.RetainsThrough ||
            eligibility.ActiveHoldIds is null || eligibility.ActiveHoldIds.Count != 0 ||
            eligibility.HistoricalRelianceIds is null || eligibility.HistoricalRelianceIds.Count != 0 ||
            !eligibility.StorageDispositionReady)
            return new(RequestErrorKind.Conflict,
                "Disposition is blocked until retention elapsed, all holds and reliance ended, and storage is ready.");
        return null;
    }

    static RequestError? ValidateState(ArtifactDispositionView state)
    {
        if (state.ArtifactId == Uuid.Empty || state.Source is null ||
            state.Source.TenantId != state.TenantId || state.Source.SourceId != state.ArtifactId ||
            !ValidDigest(state.Source.ContentSha256) || state.SourcePosition == 0 || state.ContentLength <= 0 ||
            !HandlingClasses.Contains(state.HandlingClass) || state.HandlingPolicyRevision <= 0 ||
            !state.RetentionBasisRecorded || !RetentionPolicies.Contains(state.RetentionPolicy) ||
            state.PeriodStart > state.PeriodEnd || state.RetainsThrough != state.PeriodEnd.AddYears(7) ||
            state.ApprovedAsOf <= state.RetainsThrough || state.ActiveHoldIds is null ||
            state.ActiveHoldIds.Count != 0 || state.HistoricalRelianceIds is null ||
            state.HistoricalRelianceIds.Count != 0 || !Bounded(state.Rationale, 1, MaximumRationaleLength) ||
            !ValidMember(state.ApprovedBy) || state.ApprovedAt == default ||
            state.Attempts is null || state.Attempts.Count > MaximumAttempts ||
            state.Status is not ("approved" or "executing" or "failed" or "disposed") ||
            state.Status == "approved" && state.Attempts.Count != 0 ||
            state.Status == "executing" && (state.Attempts.Count == 0 ||
                state.Attempts[^1].Status != "requested") ||
            state.Status == "failed" && (state.Attempts.Count == 0 ||
                state.Attempts[^1].Status != "failed") ||
            state.Status == "disposed" && (state.Attempts.Count == 0 ||
                state.Attempts[^1].Status != "disposed") ||
            state.Status == "disposed" != (state.Tombstone is not null))
            return new(RequestErrorKind.Validation, "The retained artifact disposition state is invalid.");
        for (var index = 0; index < state.Attempts.Count; index++)
        {
            var attempt = state.Attempts[index];
            if (attempt.Number != index + 1 || !ValidActor(attempt.RequestedBy) ||
                attempt.RequestedAt == default || attempt.SourcePosition == 0 ||
                attempt.Status is not ("requested" or "failed" or "disposed") ||
                attempt.Status == "requested" && (attempt.Outcome is not null || attempt.CompletedAt is not null ||
                    attempt.CompletedBy is not null) ||
                attempt.Status == "failed" && (attempt.Outcome != "failed" ||
                    !Bounded(attempt.FailureCode, 1, MaximumFailureCodeLength) ||
                    !ValidActor(attempt.CompletedBy) || attempt.CompletedAt is null) ||
                attempt.Status == "disposed" && (attempt.Outcome != "disposed" ||
                    attempt.FailureCode is not null || !ValidActor(attempt.CompletedBy) ||
                    attempt.CompletedAt is null))
                return new(RequestErrorKind.Validation, "A disposition attempt is invalid.");
        }
        if (state.Tombstone is { } tombstone &&
            (tombstone.ArtifactId != state.ArtifactId ||
             tombstone.ContentSha256 != state.Source.ContentSha256 ||
             tombstone.ContentLength != state.ContentLength ||
             tombstone.RetentionPolicy != state.RetentionPolicy ||
             tombstone.PeriodStart != state.PeriodStart || tombstone.PeriodEnd != state.PeriodEnd ||
             tombstone.RetainsThrough != state.RetainsThrough ||
             tombstone.ApprovalRationale != state.Rationale || tombstone.ApprovedBy != state.ApprovedBy ||
             tombstone.ApprovedAt != state.ApprovedAt || state.Attempts.Count == 0 ||
             tombstone.DisposedBy != state.Attempts[^1].CompletedBy ||
             tombstone.DisposedAt != state.Attempts[^1].CompletedAt))
            return new(RequestErrorKind.Validation, "The disposition tombstone is invalid.");
        return null;
    }

    static ArtifactDispositionView FromEligibility(ArtifactDispositionEligibilitySnapshot source,
        Uuid dispositionId, string rationale, ActorReference approvedBy, DateTimeOffset approvedAt) =>
        new(source.Source.TenantId, dispositionId, source.Source.SourceId, "approved", 1,
            source.Source with { }, source.Position, source.ContentLength, source.ArtifactState,
            source.StateReason, source.HandlingPolicyRevision, source.HandlingClass,
            source.RetentionBasisRecorded,
            source.RetentionPolicy, source.PeriodStart, source.PeriodEnd, source.RetainsThrough,
            source.AsOf, FreezeIds(source.ActiveHoldIds), FreezeIds(source.HistoricalRelianceIds),
            rationale, approvedBy, approvedAt, [], null, approvedAt);

    static bool SameEligibility(ArtifactDispositionView state,
        ArtifactDispositionEligibilitySnapshot eligibility) => eligibility is not null &&
        state.Source == eligibility.Source && state.ContentLength == eligibility.ContentLength &&
        state.ArtifactState == eligibility.ArtifactState && state.StateReason == eligibility.StateReason &&
        state.HandlingPolicyRevision == eligibility.HandlingPolicyRevision &&
        state.HandlingClass == eligibility.HandlingClass &&
        state.RetentionBasisRecorded == eligibility.RetentionBasisRecorded &&
        state.RetentionPolicy == eligibility.RetentionPolicy && state.PeriodStart == eligibility.PeriodStart &&
        state.PeriodEnd == eligibility.PeriodEnd && state.RetainsThrough == eligibility.RetainsThrough &&
        state.ActiveHoldIds.SequenceEqual(eligibility.ActiveHoldIds) &&
        state.HistoricalRelianceIds.SequenceEqual(eligibility.HistoricalRelianceIds) &&
        eligibility.Position >= state.SourcePosition && eligibility.AsOf >= state.ApprovedAsOf &&
        eligibility.StorageDispositionReady;

    static bool SameApproval(ArtifactDispositionView first, ArtifactDispositionView second) =>
        first.TenantId == second.TenantId && first.DispositionId == second.DispositionId &&
        first.ArtifactId == second.ArtifactId && first.Source == second.Source &&
        first.ContentLength == second.ContentLength && first.ArtifactState == second.ArtifactState &&
        first.StateReason == second.StateReason &&
        first.HandlingPolicyRevision == second.HandlingPolicyRevision &&
        first.HandlingClass == second.HandlingClass &&
        first.RetentionBasisRecorded == second.RetentionBasisRecorded &&
        first.RetentionPolicy == second.RetentionPolicy && first.PeriodStart == second.PeriodStart &&
        first.PeriodEnd == second.PeriodEnd && first.RetainsThrough == second.RetainsThrough &&
        first.Rationale == second.Rationale && first.ApprovedBy == second.ApprovedBy &&
        first.ApprovedAt == second.ApprovedAt &&
        first.ActiveHoldIds.SequenceEqual(second.ActiveHoldIds) &&
        first.HistoricalRelianceIds.SequenceEqual(second.HistoricalRelianceIds);

    static Uuid? TransitionActor(ArtifactDispositionView state, string transition) => transition switch
    {
        "approved" => MemberId(state.ApprovedBy),
        "executing" => state.Attempts.Count == 0 ? null : MemberId(state.Attempts[^1].RequestedBy),
        "failed" or "disposed" => state.Attempts.Count == 0 ||
            state.Attempts[^1].CompletedBy is not { } completedBy
                ? null : MemberId(completedBy),
        _ => null,
    };

    static ArtifactDispositionView FreezeView(ArtifactDispositionView state) => state with
    {
        Source = state.Source with { },
        ActiveHoldIds = FreezeIds(state.ActiveHoldIds),
        HistoricalRelianceIds = FreezeIds(state.HistoricalRelianceIds),
        Attempts = Array.AsReadOnly(state.Attempts.ToArray()),
    };

    static ReadOnlyCollection<ArtifactDispositionAttemptView> Append(
        IReadOnlyList<ArtifactDispositionAttemptView> attempts, ArtifactDispositionAttemptView attempt) =>
        Array.AsReadOnly(attempts.Append(attempt).ToArray());

    static ReadOnlyCollection<ArtifactDispositionAttemptView> ReplaceLast(
        IReadOnlyList<ArtifactDispositionAttemptView> attempts, ArtifactDispositionAttemptView last) =>
        Array.AsReadOnly(attempts.Take(attempts.Count - 1).Append(last).ToArray());

    static ReadOnlyCollection<Uuid> FreezeIds(IEnumerable<Uuid> ids) =>
        Array.AsReadOnly(ids.OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray());

    static Uuid? MemberId(ActorReference actor) => Uuid.TryParse(actor.Id, out var id) ? id : null;

    static bool ValidMember(ActorReference actor) => actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var memberId) && memberId != Uuid.Empty;

    static bool ValidActor(ActorReference? actor) => actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var memberId) && memberId != Uuid.Empty ||
        actor is { Kind: "system_process", Display.Length: > 0 and <= 200 } &&
        actor.Id.StartsWith("reactor:", StringComparison.Ordinal);

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The artifact disposition was not found."));

    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    Result StaleRevision() => Conflict($"The disposition revision is {Revision}.");
}
