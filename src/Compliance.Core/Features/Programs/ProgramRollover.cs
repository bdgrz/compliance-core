using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>
/// Owns a reviewed carry-forward plan for one completed Type II period. It records successor
/// identities for the owning workflows to create; it never copies historical occurrences or evidence.
/// </summary>
public sealed class ProgramRollover : Aggregate
{
    const int MaximumRecords = 2000;
    const int MaximumCommitments = 1000;
    const int MaximumVersions = 32;
    const int MaximumEventBytes = 256 * 1024;
    const int MaximumTextLength = 4000;
    const int MaximumComparisonMeasures = 5;
    static readonly HashSet<string> RecordTypes = new(StringComparer.Ordinal)
    {
        "control", "policy", "risk", "vendor", "integration", "finding",
        "responsibility", "schedule", "system", "application"
    };
    static readonly HashSet<string> Dispositions = new(StringComparer.Ordinal)
    {
        "carried", "changed", "retired", "unresolved", "ineligible"
    };
    static readonly HashSet<string> ComparisonMeasures = new(StringComparer.Ordinal)
    {
        "readiness", "control_pass_rate", "open_findings", "overdue_work", "evidence_completeness"
    };
    static readonly HashSet<string> ApproverRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "compliance_lead", "program_owner", "ceo", "ciso"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    ProgramRolloverView? _current;

    public long Revision => _current?.Revision ?? 0;
    public ProgramRolloverView? Current => _current;

    public ProgramRollover(Uuid tenantId, Uuid programId, Uuid rolloverId)
        : base(rolloverId, new EventStreamAddress(tenantId.ToString(), "program-rollovers",
            rolloverId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ProgramRolloverProposed>(Apply);
        On<ProgramRolloverRevised>(Apply);
        On<ProgramRolloverApproved>(Apply);
        On<ProgramRolloverDecisionRecorded>(Apply);
        On<ProgramNextPeriodCreated>(Apply);
    }

    public CommandFailure? Propose(ProgramRolloverContent content, TypeIIPeriodView sourcePeriod,
        AuditOutcomeView outcome, Uuid actorMemberId, string actorDisplay, DateTimeOffset proposedAt)
    {
        if (_current is { } existing)
            return SameProposal(existing, content, sourcePeriod, outcome, actorMemberId,
                actorDisplay, proposedAt) ? null : CommandFailure.StateConflict(
                "The rollover identity already has different source records or carry-forward choices.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, proposedAt) ||
            NormalizeContent(content) is not { } normalized ||
            !ValidCompletedSources(normalized, sourcePeriod, outcome))
            return Invalid("A rollover requires a closed Type II period, its exact auditor outcome, and explicit carry-forward decisions.");
        var version = NewVersion(normalized, 1, actorMemberId, actorDisplay.Trim(), proposedAt, "");
        var state = new ProgramRolloverView(_tenantId, _programId, Id, "proposed", 1,
            version, [version], [], null, null, null, actorMemberId, actorDisplay.Trim(), proposedAt);
        return Emit(state, actorMemberId, proposedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ProgramRolloverProposed(tenant, subject, revision, actor, snapshot, at),
            "propose", creating: true);
    }

    public CommandFailure? Revise(long expectedRevision, ProgramRolloverContent content,
        TypeIIPeriodView sourcePeriod, AuditOutcomeView outcome, string changeReason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The program rollover was not found.");
        changeReason = Clean(changeReason);
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "period_created" || current.Versions.Count >= MaximumVersions ||
            !Bounded(changeReason, 1, MaximumTextLength) || !ValidActor(actorMemberId, actorDisplay, revisedAt) ||
            revisedAt < current.UpdatedAt || NormalizeContent(content) is not { } normalized ||
            !ValidCompletedSources(normalized, sourcePeriod, outcome) ||
            normalized.SourcePeriodId != current.Content.SourcePeriodId ||
            normalized.SourceCloseSnapshotId != current.Content.SourceCloseSnapshotId ||
            normalized.SourceCloseSnapshotSha256 != current.Content.SourceCloseSnapshotSha256 ||
            normalized.AuditOutcomeId != current.Content.AuditOutcomeId ||
            normalized.AuditOutcomeContentSha256 != current.Content.AuditOutcomeContentSha256)
            return Invalid("A rollover revision must preserve the completed source and precede successor creation.");
        if (Digest(normalized) == current.CurrentVersion.ContentSha256)
            return CommandFailure.StateConflict("A rollover revision must change at least one reviewed decision.");
        var version = NewVersion(normalized, current.Versions.Count + 1, actorMemberId,
            actorDisplay.Trim(), revisedAt, changeReason);
        var state = current with
        {
            Status = "proposed",
            Revision = Revision + 1,
            CurrentVersion = version,
            Versions = Append(current.Versions, version),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = revisedAt,
        };
        return Emit(state, actorMemberId, revisedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ProgramRolloverRevised(tenant, subject, revision, actor, snapshot, at),
            "revise");
    }

    public CommandFailure? Approve(long expectedRevision, string decision, string rationale,
        Uuid approverMemberId, string approverRole, string approverDisplay, DateTimeOffset approvedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The program rollover was not found.");
        decision = Clean(decision).ToLowerInvariant();
        rationale = Clean(rationale);
        approverRole = Clean(approverRole);
        if (current.Approvals.Count > 0 && current.Approvals[^1].Version == current.CurrentVersion.Version &&
            current.Approvals[^1] is { } prior)
            return prior.Decision == decision && prior.Rationale == rationale &&
                prior.ApprovedByMemberId == approverMemberId && prior.ApproverRole == approverRole &&
                prior.ApproverDisplay == Clean(approverDisplay) && prior.ApprovedAt == approvedAt
                ? null : CommandFailure.StateConflict("A rollover version has one immutable decision.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "proposed" || current.CreatedPeriodId is not null ||
            decision is not ("approved" or "rejected") || !Bounded(rationale, 1, MaximumTextLength) ||
            !ApproverRoles.Contains(approverRole) || !ValidActor(approverMemberId, approverDisplay, approvedAt) ||
            approvedAt < current.CurrentVersion.ProposedAt)
            return Invalid("A rollover decision requires a proposed version, authorized approver, and rationale.");
        if (approverMemberId == current.CurrentVersion.ProposedByMemberId)
            return CommandFailure.ActorProhibited("The rollover proposer cannot approve or reject their own proposal.");
        var approval = new ProgramRolloverApprovalView(current.CurrentVersion.Version,
            decision, rationale, current.CurrentVersion.ContentSha256,
            current.CurrentVersion.ProposedByMemberId, approverMemberId, approverRole,
            approverDisplay.Trim(), approvedAt);
        var state = current with
        {
            Status = decision,
            Revision = Revision + 1,
            Approvals = Append(current.Approvals, approval),
            ActorMemberId = approverMemberId,
            ActorDisplay = approverDisplay.Trim(),
            UpdatedAt = approvedAt,
        };
        if (decision == "approved")
            return Emit(state, approverMemberId, approvedAt,
                static (tenant, subject, revision, actor, snapshot, at) =>
                    new ProgramRolloverApproved(tenant, subject, revision, actor, snapshot, at),
                "approve");
        return Emit(state, approverMemberId, approvedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ProgramRolloverDecisionRecorded(tenant, subject, revision, actor, snapshot, at),
            "reject");
    }

    /// <summary>Records the exact successor-period receipt after the owning period workflow succeeds.</summary>
    public CommandFailure? RecordNextPeriodCreated(long expectedRevision, Uuid periodId,
        Uuid typeIIPlanId, DateTimeOffset createdAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The program rollover was not found.");
        if (current.CreatedPeriodId == periodId && current.CreatedTypeIIPlanId == typeIIPlanId &&
            current.NextPeriodCreatedAt == createdAt)
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "approved" || current.Approvals.Count == 0 ||
            current.Approvals[^1].Decision != "approved" ||
            periodId != current.Content.NextPeriodId || typeIIPlanId != current.Content.NextTypeIIPlanId ||
            !ValidActor(current.ActorMemberId, current.ActorDisplay, createdAt) ||
            createdAt < current.Approvals[^1].ApprovedAt)
            return CommandFailure.StateConflict("Only the exact approved successor period and plan can complete a rollover.");
        var state = current with
        {
            Status = "period_created",
            Revision = Revision + 1,
            CreatedPeriodId = periodId,
            CreatedTypeIIPlanId = typeIIPlanId,
            NextPeriodCreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        return Emit(state, current.ActorMemberId, createdAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ProgramNextPeriodCreated(tenant, subject, revision, actor, snapshot, at),
            "period_created");
    }

    void Apply(ProgramRolloverProposed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "propose");
    void Apply(ProgramRolloverRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "revise");
    void Apply(ProgramRolloverApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "approve");
    void Apply(ProgramRolloverDecisionRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "reject");
    void Apply(ProgramNextPeriodCreated ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "period_created");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        ProgramRolloverView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.ProgramRolloverId != Id ||
            state.Revision != revision || state.UpdatedAt != occurredAt || occurredAt == default ||
            revision != Revision + 1 || creating != (_current is null) || actorMemberId == Uuid.Empty ||
            state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "propose" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The program-rollover event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(ProgramRolloverView state, Uuid actorMemberId, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, ProgramRolloverView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null ||
                !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The program-rollover transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return Invalid("The complete program-rollover state exceeds its bounded event size.");
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(ProgramRolloverView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId || state.ProgramRolloverId != Id ||
            state.Revision <= 0 || state.Versions is null || state.Versions.Count is < 1 or > MaximumVersions ||
            state.CurrentVersion is null || state.CurrentVersion.Version != state.Versions.Count ||
            state.Approvals is null || state.Approvals.Count > state.Versions.Count ||
            state.Versions.Where((version, index) => version.Version != index + 1 ||
                NormalizeContent(version.Content) is not { } content || Digest(content) != version.ContentSha256 ||
                version.ProposedByMemberId == Uuid.Empty || !Bounded(version.ProposedByDisplay, 1, 200) ||
                version.ProposedAt == default || !Bounded(version.ChangeReason, 0, MaximumTextLength)).Any() ||
            !SameVersion(state.CurrentVersion, state.Versions[^1]) ||
            state.Approvals.Select(item => item.Version).Distinct().Count() != state.Approvals.Count ||
            state.Approvals.Any(item => item.Version < 1 || item.Version > state.Versions.Count ||
                item.Decision is not ("approved" or "rejected") ||
                !Bounded(item.Rationale, 1, MaximumTextLength) ||
                item.ContentSha256 != state.Versions[item.Version - 1].ContentSha256 ||
                item.ProposedByMemberId != state.Versions[item.Version - 1].ProposedByMemberId ||
                item.ApprovedByMemberId == Uuid.Empty || item.ApprovedByMemberId == item.ProposedByMemberId ||
                !ApproverRoles.Contains(item.ApproverRole) || !Bounded(item.ApproverDisplay, 1, 200) ||
                item.ApprovedAt < state.Versions[item.Version - 1].ProposedAt) ||
            state.CreatedPeriodId == Uuid.Empty || state.CreatedTypeIIPlanId == Uuid.Empty ||
            (state.CreatedPeriodId is null) != (state.CreatedTypeIIPlanId is null) ||
            (state.CreatedPeriodId is null) != (state.NextPeriodCreatedAt is null) ||
            state.CreatedPeriodId is { } createdPeriod && createdPeriod != state.Content.NextPeriodId ||
            state.CreatedTypeIIPlanId is { } createdPlan && createdPlan != state.Content.NextTypeIIPlanId ||
            state.NextPeriodCreatedAt is { } createdAt && createdAt == default ||
            !ValidStatus(state) || !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt))
            return Invalid("The program-rollover event contains an invalid state snapshot.");
        return null;
    }

    static bool ValidTransition(ProgramRolloverView prior, ProgramRolloverView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.ProgramRolloverId != prior.ProgramRolloverId ||
            !Prefix(prior.Versions, next.Versions) || !Prefix(prior.Approvals, next.Approvals))
            return false;
        return transition switch
        {
            "revise" => (prior.Status is "proposed" or "approved" or "rejected") &&
                next.Status == "proposed" && next.Versions.Count == prior.Versions.Count + 1 &&
                next.Approvals.Count == prior.Approvals.Count && next.CreatedPeriodId is null &&
                next.CurrentVersion.ProposedByMemberId == actor && next.UpdatedAt == next.CurrentVersion.ProposedAt,
            "approve" => prior.Status == "proposed" && next.Status == "approved" &&
                SameVersions(prior.Versions, next.Versions) && next.Approvals.Count == prior.Approvals.Count + 1 &&
                next.Approvals[^1].Decision == "approved" && next.Approvals[^1].ApprovedByMemberId == actor &&
                next.Approvals[^1].ApprovedAt == next.UpdatedAt && next.CreatedPeriodId is null,
            "reject" => prior.Status == "proposed" && next.Status == "rejected" &&
                SameVersions(prior.Versions, next.Versions) && next.Approvals.Count == prior.Approvals.Count + 1 &&
                next.Approvals[^1].Decision == "rejected" && next.Approvals[^1].ApprovedByMemberId == actor &&
                next.Approvals[^1].ApprovedAt == next.UpdatedAt && next.CreatedPeriodId is null,
            "period_created" => prior.Status == "approved" && next.Status == "period_created" &&
                SameVersions(prior.Versions, next.Versions) && prior.Approvals.Count == next.Approvals.Count &&
                prior.CreatedPeriodId is null && next.CreatedPeriodId == next.Content.NextPeriodId &&
                next.CreatedTypeIIPlanId == next.Content.NextTypeIIPlanId &&
                next.NextPeriodCreatedAt == next.UpdatedAt,
            _ => false,
        };
    }

    static ProgramRolloverContent? NormalizeContent(ProgramRolloverContent? content)
    {
        if (content is null || content.SourcePeriodId == Uuid.Empty || content.SourceExaminationId == Uuid.Empty ||
            content.SourceCloseSnapshotId == Uuid.Empty || !Sha256(content.SourceCloseSnapshotSha256) ||
            content.AuditOutcomeId == Uuid.Empty || !Sha256(content.AuditOutcomeContentSha256) ||
            content.NextPeriodStart == default || content.NextPeriodEnd < content.NextPeriodStart ||
            content.NextPeriodId == Uuid.Empty || content.NextTypeIIPlanId == Uuid.Empty ||
            content.Records is null || content.Records.Count is < 1 or > MaximumRecords ||
            content.Commitments is null || content.Commitments.Count > MaximumCommitments ||
            content.ComparisonMeasures is null || content.ComparisonMeasures.Count is < 1 or > MaximumComparisonMeasures ||
            content.ComparisonMeasures.Any(item => !ComparisonMeasures.Contains(Clean(item))) ||
            content.ComparisonMeasures.Distinct(StringComparer.Ordinal).Count() != content.ComparisonMeasures.Count ||
            content.Records.Any(item => !ValidRecord(item)) ||
            content.Records.Select(item => (item.RecordType, item.SourceRecordId, item.SourceVersion)).Distinct().Count() != content.Records.Count ||
            content.Records.Where(item => item.SuccessorRecordId is not null).Select(item => item.SuccessorRecordId)
                .Distinct().Count() != content.Records.Count(item => item.SuccessorRecordId is not null) ||
            content.Commitments.Any(item => !ValidCommitment(item)) ||
            content.Commitments.Select(item => item.CommitmentId).Distinct().Count() != content.Commitments.Count)
            return null;
        return content with
        {
            SourceCloseSnapshotSha256 = NormalizeHash(content.SourceCloseSnapshotSha256),
            AuditOutcomeContentSha256 = NormalizeHash(content.AuditOutcomeContentSha256),
            Records = Freeze(content.Records.Select(item => item with
            {
                RecordType = item.RecordType.Trim().ToLowerInvariant(),
                SourceContentSha256 = NormalizeHash(item.SourceContentSha256),
                Disposition = item.Disposition.Trim().ToLowerInvariant(),
                Rationale = item.Rationale.Trim(),
            }).OrderBy(item => item.RecordType, StringComparer.Ordinal)
                .ThenBy(item => IdKey(item.SourceRecordId), StringComparer.Ordinal).ToArray()),
            Commitments = Freeze(content.Commitments.Select(item => item with
            {
                Description = item.Description.Trim(),
                SourceContentSha256 = NormalizeHash(item.SourceContentSha256),
            }).OrderBy(item => IdKey(item.CommitmentId), StringComparer.Ordinal).ToArray()),
            ComparisonMeasures = Freeze(content.ComparisonMeasures.Select(Clean)
                .Order(StringComparer.Ordinal).ToArray()),
        };
    }

    static bool ValidRecord(ProgramRolloverRecordContent item) => item is not null &&
        RecordTypes.Contains(Clean(item.RecordType).ToLowerInvariant()) && item.SourceRecordId != Uuid.Empty &&
        item.SourceVersion > 0 && Sha256(item.SourceContentSha256) &&
        Dispositions.Contains(Clean(item.Disposition).ToLowerInvariant()) &&
        (Clean(item.Disposition).ToLowerInvariant() is "carried" or "changed"
            ? item.SuccessorRecordId is { } successor && successor != Uuid.Empty && successor != item.SourceRecordId
            : item.SuccessorRecordId is null) && Bounded(item.Rationale, 1, MaximumTextLength);

    static bool ValidCommitment(ProgramRolloverCommitmentContent item) => item is not null &&
        item.CommitmentId != Uuid.Empty && Bounded(item.Description, 1, MaximumTextLength) &&
        item.OwnerMemberId != Uuid.Empty && item.DueOn != default && item.SourceEngagementId != Uuid.Empty &&
        item.SourceRecordId != Uuid.Empty && item.SourceRecordVersion > 0 && Sha256(item.SourceContentSha256);

    bool ValidCompletedSources(ProgramRolloverContent content,
        TypeIIPeriodView? sourcePeriod, AuditOutcomeView? outcome)
    {
        if (sourcePeriod is null || sourcePeriod.TenantId != _tenantId || sourcePeriod.ProgramId != _programId ||
            sourcePeriod.Status != "closed" ||
            sourcePeriod.CloseSnapshots is null || sourcePeriod.CloseSnapshots.Count == 0 ||
            sourcePeriod.CloseSnapshots[^1] is not { } snapshot ||
            snapshot.Version != sourcePeriod.CloseSnapshots.Count || snapshot.ActorMemberId == Uuid.Empty ||
            snapshot.RecordedAt == default || !Sha256(snapshot.ContentSha256) ||
            snapshot.CloseSnapshotId != content.SourceCloseSnapshotId ||
            snapshot.ContentSha256 != NormalizeHash(content.SourceCloseSnapshotSha256) ||
            sourcePeriod.PeriodId != content.SourcePeriodId || sourcePeriod.ExaminationId != content.SourceExaminationId ||
            content.NextPeriodStart <= sourcePeriod.PeriodEnd ||
            outcome is null || outcome.Content is null || outcome.Status != "recorded" ||
            outcome.TenantId != _tenantId || outcome.ProgramId != _programId ||
            outcome.ProgramId != sourcePeriod.ProgramId || outcome.AuditOutcomeId != content.AuditOutcomeId ||
            outcome.ContentSha256 != NormalizeHash(content.AuditOutcomeContentSha256) ||
            outcome.Content.ExaminationId != content.SourceExaminationId || outcome.Content.ScopeKind != "type_ii" ||
            outcome.Content.SourceKind != "auditor" || outcome.Content.SourceSnapshotId != snapshot.CloseSnapshotId ||
            outcome.Content.SourceSnapshotSha256 != snapshot.ContentSha256 || !Sha256(outcome.ContentSha256))
            return false;
        return true;
    }

    bool SameProposal(ProgramRolloverView state, ProgramRolloverContent? content,
        TypeIIPeriodView? period, AuditOutcomeView? outcome, Uuid actor, string display, DateTimeOffset at) =>
        NormalizeContent(content) is { } normalized && Digest(normalized) == state.CurrentVersion.ContentSha256 &&
        ValidCompletedSources(normalized, period, outcome) && state.CurrentVersion.ProposedByMemberId == actor &&
        state.CurrentVersion.ProposedByDisplay == Clean(display) && state.CurrentVersion.ProposedAt == at;

    static ProgramRolloverVersionView NewVersion(ProgramRolloverContent content, int version,
        Uuid actor, string display, DateTimeOffset at, string reason) =>
        new(version, content, Digest(content), actor, display, at, reason);

    static bool SameVersion(ProgramRolloverVersionView left, ProgramRolloverVersionView right) =>
        left.Version == right.Version && left.ContentSha256 == right.ContentSha256 &&
        left.ProposedByMemberId == right.ProposedByMemberId && left.ProposedByDisplay == right.ProposedByDisplay &&
        left.ProposedAt == right.ProposedAt && left.ChangeReason == right.ChangeReason;

    static bool SameVersions(IReadOnlyList<ProgramRolloverVersionView> left,
        IReadOnlyList<ProgramRolloverVersionView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => SameVersion(pair.First, pair.Second));

    static bool ValidStatus(ProgramRolloverView state)
    {
        var approval = state.Approvals.LastOrDefault(item => item.Version == state.CurrentVersion.Version);
        var status = state.CreatedPeriodId is not null ? "period_created" : approval?.Decision ?? "proposed";
        return state.Status == status && (state.CreatedPeriodId is null || approval?.Decision == "approved");
    }

    static bool ValidInitialState(ProgramRolloverView state) => state.Status == "proposed" &&
        state.Revision == 1 && state.Versions.Count == 1 && state.Approvals.Count == 0 &&
        state.CreatedPeriodId is null && state.CreatedTypeIIPlanId is null && state.NextPeriodCreatedAt is null;

    static bool Prefix<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) => prior.Count <= next.Count &&
        prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));

    static ProgramRolloverView Freeze(ProgramRolloverView state) => state with
    {
        Versions = Array.AsReadOnly(state.Versions.ToArray()),
        Approvals = Array.AsReadOnly(state.Approvals.ToArray()),
    };

    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static string IdKey(Uuid id) => id.ToString();
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int min, int max) => value is not null && value.Trim().Length >= min && value.Trim().Length <= max;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty && Bounded(display, 1, 200) && at != default;
    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("program rollover", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) => Freeze(values.Append(value).ToArray());
}
