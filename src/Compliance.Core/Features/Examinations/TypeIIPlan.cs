using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
/// Owns the pre-period Type II operating plan and its review and executive approval.
/// It references shared controls and exact Type I inputs; it does not copy their lifecycle state.
/// A period later binds the approved plan version as its opening authority.
/// </summary>
public sealed class TypeIIPlan : Aggregate
{
    const int MaximumVersions = 24;
    const int MaximumControls = 512;
    const int MaximumGaps = 1000;
    const int MaximumChanges = 512;
    const int MaximumEventBytes = 384 * 1024;
    const int MaximumTextLength = 2000;
    static readonly HashSet<string> TrustCategories = new(StringComparer.Ordinal)
        { "security", "availability", "confidentiality", "processing_integrity", "privacy" };
    static readonly HashSet<string> ExecutiveRoles = new(StringComparer.OrdinalIgnoreCase)
        { "ceo", "ciso" };
    static readonly HashSet<string> GapDispositions = new(StringComparer.Ordinal)
        { "carry", "accepted", "remediated", "monitor" };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    TypeIIPlanView? _current;

    public long Revision => _current?.Revision ?? 0;
    public TypeIIPlanView? Current => _current;

    public TypeIIPlan(Uuid tenantId, Uuid programId, Uuid planId)
        : base(planId, new EventStreamAddress(tenantId.ToString(), "type-ii-plans",
            planId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<TypeIIPlanProposed>(Apply);
        On<TypeIIPlanRevised>(Apply);
        On<TypeIIPlanReviewed>(Apply);
        On<TypeIIPlanApproved>(Apply);
    }

    public CommandFailure? Propose(TypeIIPlanContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset proposedAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId, actorDisplay, proposedAt)
                ? null
                : CommandFailure.StateConflict("The Type II plan already exists with different content.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, proposedAt))
            return Invalid("A Type II plan requires tenant, program, plan, and attributed proposer identities.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("The proposed Type II plan must bind exact sources, owned controls, cadence, and period changes.");
        if (!CompleteGapAcknowledgements(normalized))
            return CommandFailure.StateConflict("Every carried-forward gap requires an explicit, reasoned acknowledgement.");

        var version = NewVersion(normalized, 1, actorMemberId, actorDisplay.Trim(), proposedAt, "");
        var state = new TypeIIPlanView(_tenantId, _programId, Id, "proposed", 1, 1,
            version, [version], [], [], proposedAt);
        return Emit(state, actorMemberId, proposedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TypeIIPlanProposed(tenant, subject, revision, actor, snapshot, at),
            creating: true);
    }

    public CommandFailure? Revise(long expectedRevision, TypeIIPlanContent content,
        string changeReason, Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II plan was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status is "approved" or "retired" || current.Versions.Count >= MaximumVersions)
            return CommandFailure.StateConflict("An approved plan is immutable; propose a successor plan for later periods.");
        if (!ValidActor(actorMemberId, actorDisplay, revisedAt))
            return Invalid("A plan revision requires an attributed member.");
        changeReason = Clean(changeReason);
        if (!Bounded(changeReason, 1, MaximumTextLength) || NormalizeContent(content) is not { } normalized)
            return Invalid("A plan revision requires complete content and a bounded change reason.");
        if (!CompleteGapAcknowledgements(normalized))
            return CommandFailure.StateConflict("Every carried-forward gap requires an explicit, reasoned acknowledgement.");
        if (normalized.ExaminationId != current.Current!.Content.ExaminationId)
            return CommandFailure.StateConflict("A Type II plan cannot change its examination.");
        if (Digest(normalized) == current.Current!.ContentSha256)
            return CommandFailure.StateConflict("A new plan version must change its intended contents.");

        var version = NewVersion(normalized, current.CurrentVersionNumber + 1,
            actorMemberId, actorDisplay.Trim(), revisedAt, changeReason);
        var state = current with
        {
            Status = "proposed",
            Revision = Revision + 1,
            CurrentVersionNumber = version.Version,
            Current = version,
            Versions = Append(current.Versions, version),
            UpdatedAt = revisedAt,
        };
        return Emit(state, actorMemberId, revisedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TypeIIPlanRevised(tenant, subject, revision, actor, snapshot, at));
    }

    public CommandFailure? Review(long expectedRevision, int planVersion, string outcome,
        string comments, Uuid reviewerMemberId, string reviewerDisplay, DateTimeOffset reviewedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II plan was not found.");
        if (!ValidActor(reviewerMemberId, reviewerDisplay, reviewedAt))
            return Invalid("A plan review requires an attributed reviewer.");
        outcome = Clean(outcome).ToLowerInvariant();
        comments = Clean(comments);
        var version = current.Versions.FirstOrDefault(item => item.Version == planVersion);
        if (version is null)
            return CommandFailure.MissingRecord("The Type II plan version was not found.");
        if (current.Reviews.FirstOrDefault(item => item.PlanVersion == planVersion) is { } prior)
            return prior.Outcome == outcome && prior.Comments == comments &&
                   prior.ReviewerMemberId == reviewerMemberId && prior.ReviewerDisplay == reviewerDisplay.Trim() &&
                   prior.ReviewedAt == reviewedAt
                ? null
                : CommandFailure.StateConflict("A plan version has one immutable review decision.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (planVersion != current.CurrentVersionNumber || current.Status != "proposed")
            return CommandFailure.StateConflict("Only the current proposed plan version can be reviewed.");
        if (outcome is not ("approve" or "changes_requested" or "reject") ||
            !Bounded(comments, 1, MaximumTextLength))
            return Invalid("A plan review requires a supported decision and reasoned comments.");
        if (reviewerMemberId == version.ProposedByMemberId)
            return CommandFailure.ActorProhibited("The plan proposer cannot independently review the same version.");

        var review = new TypeIIPlanReviewView(planVersion, outcome, comments,
            reviewerMemberId, reviewerDisplay.Trim(), reviewedAt);
        var state = current with
        {
            Status = outcome switch
            {
                "approve" => "reviewed",
                "changes_requested" => "changes_requested",
                _ => "rejected",
            },
            Revision = Revision + 1,
            Reviews = Append(current.Reviews, review),
            UpdatedAt = reviewedAt,
        };
        return Emit(state, reviewerMemberId, reviewedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TypeIIPlanReviewed(tenant, subject, revision, actor, snapshot, at));
    }

    public CommandFailure? Approve(long expectedRevision, int planVersion, string rationale,
        Uuid approverMemberId, string approverRole, string approverDisplay,
        DateTimeOffset approvedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II plan was not found.");
        if (!ValidActor(approverMemberId, approverDisplay, approvedAt))
            return Invalid("Plan approval requires an attributed executive.");
        rationale = Clean(rationale);
        approverRole = Clean(approverRole).ToLowerInvariant();
        var version = current.Versions.FirstOrDefault(item => item.Version == planVersion);
        if (version is null)
            return CommandFailure.MissingRecord("The Type II plan version was not found.");
        var prior = current.Approvals.FirstOrDefault(item => item.PlanVersion == planVersion);
        if (prior is not null)
            return prior.Rationale == rationale && prior.ApprovedByMemberId == approverMemberId &&
                   StringComparer.OrdinalIgnoreCase.Equals(prior.ApproverRole, approverRole)
                ? null
                : CommandFailure.StateConflict("A reviewed plan version has one immutable approval.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (planVersion != current.CurrentVersionNumber || current.Status != "reviewed" ||
            current.Reviews.LastOrDefault(item => item.PlanVersion == planVersion)?.Outcome != "approve")
            return CommandFailure.StateConflict("An independent approving review is required before executive plan approval.");
        var review = current.Reviews.Single(item => item.PlanVersion == planVersion);
        if (!ExecutiveRoles.Contains(approverRole) || !Bounded(rationale, 1, MaximumTextLength))
            return Invalid("Type II plan approval requires a CEO/CISO and a bounded rationale.");
        if (approverMemberId == review.ReviewerMemberId || approverMemberId == version.ProposedByMemberId)
            return CommandFailure.ActorProhibited("The proposer and reviewer cannot approve the same Type II plan version.");

        var approval = new TypeIIPlanApprovalView(planVersion, rationale, approverMemberId,
            approverRole, approverDisplay.Trim(), approvedAt);
        var state = current with
        {
            Status = "approved",
            Revision = Revision + 1,
            Approvals = Append(current.Approvals, approval),
            UpdatedAt = approvedAt,
        };
        return Emit(state, approverMemberId, approvedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TypeIIPlanApproved(tenant, subject, revision, actor, snapshot, at));
    }

    void Apply(TypeIIPlanProposed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, creating: true);
    void Apply(TypeIIPlanRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);
    void Apply(TypeIIPlanReviewed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);
    void Apply(TypeIIPlanApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        TypeIIPlanView state, DateTimeOffset occurredAt, bool creating = false)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.TypeIIPlanId != Id ||
            state.Revision != revision || occurredAt == default || revision != Revision + 1 ||
            creating != (_current is null) || actorMemberId == Uuid.Empty ||
            ValidateState(state) is not null || _current is null &&
                state.Current?.ProposedByMemberId != actorMemberId ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId))
            throw new InvalidOperationException("The Type II plan event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(TypeIIPlanView state, Uuid actorMemberId, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, TypeIIPlanView, DateTimeOffset, DomainEvent> createEvent,
        bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null || state.Current?.ProposedByMemberId != actorMemberId
                : _current is null || !ValidTransition(_current, state, actorMemberId)))
            return Invalid("The Type II plan transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return Invalid("The complete Type II plan exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(TypeIIPlanView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId || state.TypeIIPlanId != Id ||
            state.Revision <= 0 || state.Versions is null || state.Versions.Count is < 1 or > MaximumVersions ||
            state.Current is null || state.CurrentVersionNumber != state.Current.Version ||
            state.CurrentVersionNumber != state.Versions.Count || state.Reviews is null ||
            state.Approvals is null || state.Versions.Where((version, index) => version.Version != index + 1 ||
                NormalizeContent(version.Content) is not { } content ||
                Digest(content) != Digest(version.Content) || !CompleteGapAcknowledgements(content) ||
                version.ContentSha256 != Digest(content) || version.ProposedByMemberId == Uuid.Empty ||
                !Bounded(version.ProposedByDisplay, 1, 200) || version.ProposedAt == default ||
                !Bounded(version.ChangeReason, 0, MaximumTextLength)).Any() ||
            state.Reviews.Count > state.Versions.Count ||
            state.Reviews.Select(item => item.PlanVersion).Distinct().Count() != state.Reviews.Count ||
            state.Reviews.Any(item => item.PlanVersion < 1 || item.PlanVersion > state.CurrentVersionNumber ||
                item.Outcome is not ("approve" or "changes_requested" or "reject") ||
                !Bounded(item.Comments, 1, MaximumTextLength) || item.ReviewerMemberId == Uuid.Empty ||
                !Bounded(item.ReviewerDisplay, 1, 200) || item.ReviewedAt == default) ||
            state.Approvals.Count > state.Versions.Count ||
            state.Approvals.Select(item => item.PlanVersion).Distinct().Count() != state.Approvals.Count ||
            state.Approvals.Any(item => item.PlanVersion < 1 || item.PlanVersion > state.CurrentVersionNumber ||
                !Bounded(item.Rationale, 1, MaximumTextLength) || item.ApprovedByMemberId == Uuid.Empty ||
                !ExecutiveRoles.Contains(item.ApproverRole) || !Bounded(item.ApproverDisplay, 1, 200) ||
                item.ApprovedAt == default))
            return Invalid("The Type II plan event contains an invalid state snapshot.");
        if (state.Current.Version != state.Versions[^1].Version ||
            Digest(state.Current.Content) != Digest(state.Versions[^1].Content) ||
            state.Current.ContentSha256 != state.Versions[^1].ContentSha256 ||
            state.Current.ProposedByMemberId != state.Versions[^1].ProposedByMemberId ||
            state.Current.ProposedAt != state.Versions[^1].ProposedAt)
            return Invalid("The current Type II plan version must be the last immutable version.");
        var currentVersion = state.CurrentVersionNumber;
        var status = state.Approvals.Any(item => item.PlanVersion == currentVersion) ? "approved" :
            state.Reviews.FirstOrDefault(item => item.PlanVersion == currentVersion)?.Outcome switch
            {
                "approve" => "reviewed",
                "changes_requested" => "changes_requested",
                "reject" => "rejected",
                _ => "proposed",
            };
        return state.Status == status ? null : Invalid("The Type II plan status does not match its decision history.");
    }

    static bool ValidTransition(TypeIIPlanView prior, TypeIIPlanView next, Uuid? actorMemberId)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.TypeIIPlanId != prior.TypeIIPlanId ||
            next.Versions.Count < prior.Versions.Count || next.Versions.Count > prior.Versions.Count + 1)
            return false;
        if (next.Versions.Count == prior.Versions.Count)
            return SameVersions(prior.Versions, next.Versions) &&
                   prior.Reviews.Count <= next.Reviews.Count &&
                   next.Reviews.Take(prior.Reviews.Count).SequenceEqual(prior.Reviews) &&
                   (next.Reviews.Count == prior.Reviews.Count + 1 &&
                    next.Reviews[^1].ReviewerMemberId == actorMemberId ||
                    next.Approvals.Count == prior.Approvals.Count + 1 &&
                    next.Approvals[^1].ApprovedByMemberId == actorMemberId) &&
                   prior.Approvals.Count <= next.Approvals.Count &&
                   next.Approvals.Take(prior.Approvals.Count).SequenceEqual(prior.Approvals);
        return SameVersions(prior.Versions, next.Versions.Take(prior.Versions.Count).ToArray()) &&
               next.CurrentVersionNumber == prior.CurrentVersionNumber + 1 &&
               next.Reviews.SequenceEqual(prior.Reviews) && next.Approvals.SequenceEqual(prior.Approvals) &&
               next.Current!.ProposedByMemberId == actorMemberId && next.Current.ProposedAt == next.UpdatedAt;
    }

    static TypeIIPlanContent? NormalizeContent(TypeIIPlanContent? content)
    {
        if (content is null || content.ExaminationId == Uuid.Empty || content.SourceSnapshotId == Uuid.Empty ||
            !Sha256(content.SourceSnapshotSha256) || content.SystemDescriptionVersionId == Uuid.Empty ||
            content.PeriodStart == default || content.PeriodEnd < content.PeriodStart ||
            !Bounded(content.AuditFirm, 1, 200) || content.TrustServiceCategories is null ||
            content.TrustServiceCategories.Count is < 1 or > 5 ||
            content.TrustServiceCategories.Any(item => !TrustCategories.Contains(item)) ||
            content.TrustServiceCategories.Distinct(StringComparer.Ordinal).Count() != content.TrustServiceCategories.Count ||
            content.TypeIBaselineId == Uuid.Empty || content.TypeIOutcomeId == Uuid.Empty ||
            content.CarryForwardInputs is null || content.CarryForwardInputs.Count > MaximumGaps ||
            content.Controls is null || content.Controls.Count is < 1 or > MaximumControls ||
            content.CarriedForwardGaps is null || content.CarriedForwardGaps.Count > MaximumGaps ||
            content.AcknowledgedGaps is null || content.AcknowledgedGaps.Count > MaximumGaps ||
            content.RequiredChanges is null || content.RequiredChanges.Count > MaximumChanges)
            return null;
        if (content.CarryForwardInputs.Any(item => item is null || !Token(item.SourceType, 80) ||
                item.RecordId == Uuid.Empty || item.VersionId == Uuid.Empty || item.Version <= 0 ||
                !Sha256(item.ContentSha256)) ||
            content.CarryForwardInputs.Select(item => (item.SourceType, item.RecordId, item.VersionId))
                .Distinct().Count() != content.CarryForwardInputs.Count ||
            content.Controls.Any(item => item is null || item.ControlId == Uuid.Empty ||
                item.ControlVersionId == Uuid.Empty || !Bounded(item.ControlTitle, 1, 300) ||
                item.OwnerMemberId is null || item.OwnerMemberId == Uuid.Empty ||
                !Token(item.Cadence, 80) || item.ExpectedOccurrences <= 0 ||
                item.EffectiveFrom < content.PeriodStart || item.EffectiveFrom > content.PeriodEnd ||
                item.EffectiveThrough is { } through && (through < item.EffectiveFrom || through > content.PeriodEnd)) ||
            content.Controls.Select(item => (item.ControlId, item.ControlVersionId)).Distinct().Count() != content.Controls.Count ||
            content.CarriedForwardGaps.Any(item => item is null || !Token(item.SourceKind, 80) ||
                item.SourceRecordId == Uuid.Empty || item.SourceVersionId == Uuid.Empty ||
                item.SourceVersion <= 0 || !Sha256(item.SourceSha256) ||
                !GapDispositions.Contains(item.Disposition)) ||
            content.CarriedForwardGaps.Select(item => (item.SourceKind, item.SourceRecordId, item.SourceVersionId))
                .Distinct().Count() != content.CarriedForwardGaps.Count ||
            content.AcknowledgedGaps.Any(item => item is null || item.GapId == Uuid.Empty ||
                !Bounded(item.Summary, 1, MaximumTextLength) || item.Status != "acknowledged" ||
                !Bounded(item.Rationale, 1, MaximumTextLength)) ||
            content.AcknowledgedGaps.Select(item => item.GapId).Distinct().Count() != content.AcknowledgedGaps.Count ||
            content.RequiredChanges.Any(item => item is null || item.ChangeId == Uuid.Empty ||
                !Bounded(item.Description, 1, MaximumTextLength) || item.RequiredBy == default) ||
            content.RequiredChanges.Select(item => item.ChangeId).Distinct().Count() != content.RequiredChanges.Count)
            return null;
        return content with
        {
            SourceSnapshotSha256 = NormalizeHash(content.SourceSnapshotSha256),
            AuditFirm = content.AuditFirm.Trim(),
            TrustServiceCategories = Freeze(content.TrustServiceCategories.Order(StringComparer.Ordinal).ToArray()),
            CarryForwardInputs = Freeze(content.CarryForwardInputs.Select(item => item with
            {
                SourceType = item.SourceType.Trim(),
                ContentSha256 = NormalizeHash(item.ContentSha256),
            }).OrderBy(item => item.SourceType, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ToArray()),
            Controls = Freeze(content.Controls.Select(item => item with
            {
                ControlTitle = item.ControlTitle.Trim(),
                Cadence = item.Cadence.Trim().ToLowerInvariant(),
            }).OrderBy(item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.ControlVersionId.ToString(), StringComparer.Ordinal).ToArray()),
            CarriedForwardGaps = Freeze(content.CarriedForwardGaps.Select(item => item with
            {
                SourceKind = item.SourceKind.Trim(),
                SourceSha256 = NormalizeHash(item.SourceSha256),
                Disposition = item.Disposition.Trim(),
            }).OrderBy(item => item.SourceKind, StringComparer.Ordinal)
                .ThenBy(item => item.SourceRecordId.ToString(), StringComparer.Ordinal).ToArray()),
            AcknowledgedGaps = Freeze(content.AcknowledgedGaps.Select(item => item with
            {
                Summary = item.Summary.Trim(),
                Rationale = item.Rationale.Trim(),
            }).OrderBy(item => item.GapId.ToString(), StringComparer.Ordinal).ToArray()),
            RequiredChanges = Freeze(content.RequiredChanges.Select(item => item with
            { Description = item.Description.Trim() }).OrderBy(item => item.ChangeId.ToString(), StringComparer.Ordinal).ToArray()),
        };
    }

    static bool CompleteGapAcknowledgements(TypeIIPlanContent content) =>
        content.CarriedForwardGaps.Select(item => item.SourceRecordId).OrderBy(item => item.ToString(), StringComparer.Ordinal)
            .SequenceEqual(content.AcknowledgedGaps.Select(item => item.GapId)
                .OrderBy(item => item.ToString(), StringComparer.Ordinal));

    static TypeIIPlanVersionView NewVersion(TypeIIPlanContent content, int version,
        Uuid actor, string display, DateTimeOffset at, string reason) =>
        new(version, content, Digest(content), actor, display, at, reason);

    static bool SameVersions(IReadOnlyList<TypeIIPlanVersionView> left,
        IReadOnlyList<TypeIIPlanVersionView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => pair.First.Version == pair.Second.Version &&
            pair.First.ContentSha256 == pair.Second.ContentSha256 &&
            pair.First.ProposedByMemberId == pair.Second.ProposedByMemberId &&
            pair.First.ProposedByDisplay == pair.Second.ProposedByDisplay &&
            pair.First.ProposedAt == pair.Second.ProposedAt &&
            pair.First.ChangeReason == pair.Second.ChangeReason);

    static bool SameInitial(TypeIIPlanView state, TypeIIPlanContent? content,
        Uuid actor, string display, DateTimeOffset at) =>
        NormalizeContent(content) is { } normalized && state.Current is { } version &&
        Digest(normalized) == version.ContentSha256 && version.ProposedByMemberId == actor &&
        version.ProposedByDisplay == display.Trim() && at != default && at >= version.ProposedAt;

    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty &&
        Bounded(display, 1, 200) && at != default;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("Type II plan", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool Token(string? value, int maximum) => Bounded(value, 1, maximum) &&
        value!.Trim().All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static TypeIIPlanView Freeze(TypeIIPlanView state)
    {
        var versions = Freeze(state.Versions.Select(item => item with
        { Content = NormalizeContent(item.Content)! }).ToArray());
        return state with
        {
            Current = versions[^1],
            Versions = versions,
            Reviews = Freeze(state.Reviews.ToArray()),
            Approvals = Freeze(state.Approvals.ToArray()),
        };
    }
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) =>
        Freeze(values.Append(value).ToArray());
}
