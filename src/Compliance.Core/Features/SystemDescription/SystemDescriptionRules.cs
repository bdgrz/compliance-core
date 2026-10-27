using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>
///     A transient rules/read composition reconstructs immutable narrative versions,
///     their review and approval decisions, snapshot bindings, and export identities.
/// </summary>
public sealed class SystemDescriptionRules
{
    const int MaximumSections = 32;
    const int MaximumTitleLength = 200;
    const int MaximumSectionTextLength = 12_000;
    const int MaximumDecisionTextLength = 4000;
    const int MaximumActorDisplayLength = 200;
    static readonly Uuid DecisionNamespace = Uuid.Parse(
        "497b8fc1-c9de-5e5b-8a93-f5a6972f2537",
        System.Globalization.CultureInfo.InvariantCulture);
    static readonly HashSet<string> RequiredSectionIds = new(StringComparer.Ordinal)
    {
        "services", "commitments_requirements", "components_infrastructure_software",
        "people", "procedures", "information_data_flows", "system_boundary",
        "control_environment", "user_entity_controls", "subservice_organizations",
        "exclusions", "relevant_changes",
    };

    readonly Dictionary<Type, Action<object>> _reducers = [];
    readonly List<object> _applied = [];
    readonly List<object> _pending = [];
    public Uuid Id { get; }
    public EventStreamAddress Stream { get; }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public long SourceNativeSequence { get; internal set; }
    public ulong SourcePhysicalPosition { get; internal set; }
    public IReadOnlyList<object> AppliedChanges => _applied.AsReadOnly();
    public IReadOnlyList<object> PendingChanges => _pending.AsReadOnly();
    public void ClearPendingChanges() => _pending.Clear();
    void On<T>(Action<T> reducer) where T : class => _reducers.Add(typeof(T), value => reducer((T)value));
    public void Apply(object value)
    {
        if (!_reducers.TryGetValue(value.GetType(), out var reduce))
            throw new InvalidOperationException("The description rule mutation has another kind.");
        reduce(value);
        _applied.Add(value);
    }
    internal void RaiseEvent(object value)
    {
        Apply(value);
        _pending.Add(value);
    }

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<SystemDescriptionVersionView> _versions = [];
    readonly List<SystemDescriptionReviewView> _reviews = [];
    readonly List<SystemDescriptionApprovalView> _approvals = [];
    readonly List<SystemDescriptionNoChangeDecisionView> _noChangeDecisions = [];
    readonly List<SystemDescriptionBindingView> _bindings = [];

    public long Revision { get; private set; }

    public SystemDescriptionVersionView? CurrentVersion => _versions.LastOrDefault();

    public IReadOnlyList<SystemDescriptionReviewView> Reviews =>
        Array.AsReadOnly(_reviews.ToArray());

    public IReadOnlyList<SystemDescriptionNoChangeDecisionView> NoChangeDecisions =>
        Array.AsReadOnly(_noChangeDecisions.ToArray());

    public SystemDescriptionRules(Uuid tenantId, Uuid programId, Uuid descriptionId)
    {
        Id = descriptionId;
        Stream = new EventStreamAddress(tenantId.ToString(), SystemDescriptionAggregate.Area, descriptionId.ToString());
        _tenantId = tenantId;
        _programId = programId;
        On<SystemDescriptionDraftCreatedBody>(Apply);
        On<SystemDescriptionDraftRevisedBody>(Apply);
        On<SystemDescriptionSourcesBoundBody>(Apply);
        On<SystemDescriptionReviewedBody>(Apply);
        On<SystemDescriptionApprovedBody>(Apply);
        On<SystemDescriptionSuccessorProposedBody>(Apply);
        On<SystemDescriptionNoChangeRecordedBody>(Apply);
        On<SystemDescriptionBoundBody>(Apply);
    }

    public SystemDescriptionView Read() => new(_tenantId, _programId, Id, Revision,
        CurrentVersion, Array.AsReadOnly(_versions.ToArray()), Array.AsReadOnly(_reviews.ToArray()),
        Array.AsReadOnly(_approvals.ToArray()), Array.AsReadOnly(_noChangeDecisions.ToArray()),
        Array.AsReadOnly(_bindings.ToArray()));

    public SystemDescriptionVersionView? FindVersion(long version) =>
        _versions.FirstOrDefault(item => item.Version == version);

    public SystemDescriptionVersionView? FindVersion(Uuid versionId) =>
        _versions.FirstOrDefault(item => item.SystemDescriptionVersionId == versionId);

    public SystemDescriptionApprovalView? FindApproval(Uuid versionId) =>
        _approvals.FirstOrDefault(item => item.VersionId == versionId);

    internal static SystemDescriptionRules FromView(SystemDescriptionView before)
    {
        var rules = new SystemDescriptionRules(before.TenantId, before.ProgramId, before.SystemDescriptionId);
        rules._versions.AddRange(before.Versions.Select(FreezeVersion));
        rules._reviews.AddRange(before.Reviews);
        rules._approvals.AddRange(before.Approvals);
        rules._noChangeDecisions.AddRange(before.NoChangeDecisions);
        rules._bindings.AddRange(before.Bindings);
        rules.Revision = before.Revision;
        return rules;
    }

    public CommandFailure? Create(Uuid versionId, SystemDescriptionScope scope,
        SystemDescriptionDraftContent content, Uuid authorMemberId, string authorDisplay,
        DateTimeOffset createdAt)
    {
        if (CurrentVersion is { } existing)
            return versionId == existing.SystemDescriptionVersionId &&
                   scope == existing.Scope && ContentHash(scope, content) == existing.ContentSha256
                ? null
                : CommandFailure.StateConflict(
                    "The system description already exists with different initial content.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            versionId == Uuid.Empty)
            return Invalid("A system description requires tenant, program, description, and version identities.");
        if (ValidateActor(authorMemberId, authorDisplay, createdAt) is { } actorFailure)
            return actorFailure;
        if (NormalizeScope(scope) is not { } normalizedScope ||
            NormalizeContent(content) is not { } normalizedContent)
            return Invalid("The description requires a valid Type I baseline or Type II period scope and structured sections.");

        var version = BuildVersion(versionId, null, normalizedScope, normalizedContent,
            "Initial draft", authorMemberId, authorDisplay.Trim(), createdAt);
        var ev = new SystemDescriptionDraftCreatedBody(_tenantId, _programId, Id, 1, version,
            createdAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? Revise(long expectedRevision, Uuid versionId,
        SystemDescriptionDraftContent content, string changeReason, Uuid authorMemberId,
        string authorDisplay, DateTimeOffset revisedAt)
    {
        if (CurrentVersion is not { } current)
            return CommandFailure.MissingRecord("The system description was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("system description", Revision));
        if (current.Status != "draft")
            return CommandFailure.StateConflict(
                "An approved or superseded description is immutable; propose a successor draft.");
        if (ValidateActor(authorMemberId, authorDisplay, revisedAt) is { } actorFailure)
            return actorFailure;
        var cleanReason = changeReason?.Trim() ?? string.Empty;
        if (!BoundedText(cleanReason, MaximumDecisionTextLength) ||
            NormalizeContent(content) is not { } normalizedContent)
            return Invalid("A description revision requires valid structured content and a bounded reason.");
        if (versionId == Uuid.Empty || _versions.Any(item =>
                item.SystemDescriptionVersionId == versionId))
            return CommandFailure.StateConflict("A system description version identity cannot be reused.");

        var version = BuildVersion(versionId, current.SystemDescriptionVersionId, current.Scope,
            normalizedContent, cleanReason, authorMemberId, authorDisplay.Trim(), revisedAt,
            current.Version + 1);
        var ev = new SystemDescriptionDraftRevisedBody(_tenantId, _programId, Id, Revision + 1,
            version, revisedAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? BindSources(long expectedRevision, Uuid versionId,
        IReadOnlyList<SystemDescriptionSourceReference> sources, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset boundAt)
    {
        if (CurrentVersion is not { } current)
            return CommandFailure.MissingRecord("The system description was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("system description", Revision));
        if (current.Status != "draft")
            return CommandFailure.StateConflict(
                "Sources can only be refreshed on a draft version; approved descriptions are immutable.");
        if (ValidateActor(actorMemberId, actorDisplay, boundAt) is { } actorFailure)
            return actorFailure;
        var content = NormalizeContent(current.Content with { Sources = sources });
        if (content is null)
            return Invalid("Every bound source must identify one exact governed version and reconciliation state.");
        if (versionId == Uuid.Empty || _versions.Any(item =>
                item.SystemDescriptionVersionId == versionId))
            return CommandFailure.StateConflict("A system description version identity cannot be reused.");

        var version = BuildVersion(versionId, current.SystemDescriptionVersionId, current.Scope,
            content, "Governed sources refreshed", actorMemberId, actorDisplay.Trim(), boundAt,
            current.Version + 1);
        var ev = new SystemDescriptionSourcesBoundBody(_tenantId, _programId, Id, Revision + 1,
            version, boundAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? ProposeSuccessor(long expectedRevision, Uuid versionId,
        SystemDescriptionDraftContent content, string changeReason, Uuid authorMemberId,
        string authorDisplay, DateTimeOffset proposedAt)
    {
        if (CurrentVersion is not { Status: "approved" } current)
            return CommandFailure.StateConflict(
                "A system description successor requires a currently approved version.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("system description", Revision));
        if (ValidateActor(authorMemberId, authorDisplay, proposedAt) is { } actorFailure)
            return actorFailure;
        var cleanReason = changeReason?.Trim() ?? string.Empty;
        if (!BoundedText(cleanReason, MaximumDecisionTextLength) ||
            NormalizeContent(content) is not { } normalizedContent)
            return Invalid("A successor requires valid structured content and a bounded change reason.");
        if (versionId == Uuid.Empty || _versions.Any(item =>
                item.SystemDescriptionVersionId == versionId))
            return CommandFailure.StateConflict("A system description version identity cannot be reused.");

        var version = BuildVersion(versionId, current.SystemDescriptionVersionId, current.Scope,
            normalizedContent, cleanReason, authorMemberId, authorDisplay.Trim(), proposedAt,
            current.Version + 1);
        var ev = new SystemDescriptionSuccessorProposedBody(_tenantId, _programId, Id,
            Revision + 1, version, proposedAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? Review(long version, Uuid reviewerMemberId, string reviewerDisplay,
        string outcome, string comments, DateTimeOffset reviewedAt)
    {
        if (CurrentVersion is not { } current)
            return CommandFailure.MissingRecord("The system description was not found.");
        if (version != current.Version || current.Status != "draft")
            return CommandFailure.StateConflict(
                "A review must target the current draft version exactly.");
        if (ValidateActor(reviewerMemberId, reviewerDisplay, reviewedAt) is { } actorFailure)
            return actorFailure;
        comments = comments?.Trim() ?? string.Empty;
        if (reviewerMemberId == current.AuthorMemberId)
            return CommandFailure.StateConflict(
                "The author cannot review their own system description version.");
        if (outcome is not ("approve" or "request_changes") ||
            comments.Length > MaximumDecisionTextLength ||
            outcome == "request_changes" && string.IsNullOrWhiteSpace(comments))
            return Invalid("A review requires an approve or request_changes outcome and bounded comments.");
        var reviewId = Uuid.CreateVersion5(DecisionNamespace,
            $"{Id}\n{current.SystemDescriptionVersionId}\n{reviewerMemberId}");
        if (_reviews.FirstOrDefault(item => item.ReviewId == reviewId) is { } existing)
            return existing.Outcome == outcome && existing.Comments == comments &&
                   existing.ReviewerDisplay == reviewerDisplay.Trim()
                ? null
                : CommandFailure.StateConflict(
                    "A reviewer decision is immutable for this description version.");

        var review = new SystemDescriptionReviewView(reviewId,
            current.SystemDescriptionVersionId, current.Version, outcome, comments,
            reviewerMemberId, reviewerDisplay.Trim(), reviewedAt);
        var ev = new SystemDescriptionReviewedBody(_tenantId, _programId, Id, Revision + 1,
            review, reviewedAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? Approve(long version, Uuid approverMemberId, string approverDisplay,
        string rationale, DateTimeOffset approvedAt)
    {
        if (CurrentVersion is not { } current)
            return CommandFailure.MissingRecord("The system description was not found.");
        if (version != current.Version || current.Status != "draft")
            return CommandFailure.StateConflict(
                "Approval must target the current draft version exactly.");
        if (ValidateActor(approverMemberId, approverDisplay, approvedAt) is { } actorFailure)
            return actorFailure;
        rationale = rationale?.Trim() ?? string.Empty;
        if (approverMemberId == current.AuthorMemberId)
            return CommandFailure.StateConflict(
                "The author cannot approve their own system description version.");
        if (!BoundedText(rationale, MaximumDecisionTextLength))
            return Invalid("System description approval requires a bounded rationale.");
        if (_approvals.FirstOrDefault(item => item.VersionId == current.SystemDescriptionVersionId) is { } prior)
            return prior.ApproverMemberId == approverMemberId && prior.Rationale == rationale
                ? null
                : CommandFailure.StateConflict("The system description version is already approved.");
        if (_reviews.LastOrDefault(item => item.VersionId == current.SystemDescriptionVersionId)?.Outcome
            != "approve")
            return CommandFailure.StateConflict(
                "The exact system description version requires a current independent approval review.");
        if (current.Content.Sections.Any(static section => section.Status == "incomplete"))
            return Invalid("Every required system description section must be complete or justified as not applicable.");
        if (current.Content.Sources.Any(static source => source.Status != "current"))
            return CommandFailure.StateConflict(
                "Stale, missing, or contradictory system description sources must be reconciled before approval.");
        if (current.Scope.Stage == "type_ii" && current.Scope.CloseSnapshotId is null)
            return CommandFailure.StateConflict(
                "A final Type II system description must bind its exact period-close snapshot.");
        if (current.Content.PeriodChanges.Any(change => !change.IsMaterial &&
                !_noChangeDecisions.Any(decision => decision.VersionId == current.SystemDescriptionVersionId &&
                    decision.ChangeId == change.ChangeId)))
            return CommandFailure.StateConflict(
                "Every non-material Type II change requires an attributable no-change decision.");
        if (current.Content.PeriodChanges.Any(change => change.IsMaterial &&
                !current.Content.SignificantChangeIds.Contains(change.ChangeId)))
            return CommandFailure.StateConflict(
                "Every material Type II change must be identified in the system description.");

        var approval = new SystemDescriptionApprovalView(current.SystemDescriptionVersionId,
            current.Version, approverMemberId, approverDisplay.Trim(), rationale,
            current.Scope.CloseSnapshotId, approvedAt);
        var ev = new SystemDescriptionApprovedBody(_tenantId, _programId, Id, Revision + 1,
            approval, approvedAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? RecordNoChangeDecision(long version, Uuid changeId,
        string rationale, Uuid decidedByMemberId, string decidedByDisplay,
        DateTimeOffset decidedAt)
    {
        if (CurrentVersion is not { } current)
            return CommandFailure.MissingRecord("The system description was not found.");
        if (version != current.Version || current.Status != "draft" ||
            current.Scope.Stage != "type_ii")
            return CommandFailure.StateConflict(
                "A no-change decision must target the current Type II draft version.");
        if (ValidateActor(decidedByMemberId, decidedByDisplay, decidedAt) is { } actorFailure)
            return actorFailure;
        rationale = rationale?.Trim() ?? string.Empty;
        if (decidedByMemberId == current.AuthorMemberId ||
            !BoundedText(rationale, MaximumDecisionTextLength))
            return Invalid("A no-change decision requires an independent actor and bounded rationale.");
        if (current.Content.PeriodChanges.FirstOrDefault(change => change.ChangeId == changeId) is not
            { IsMaterial: false } change)
            return CommandFailure.StateConflict(
                "A no-change decision can only disposition a recorded non-material period change.");
        var prior = _noChangeDecisions.FirstOrDefault(item => item.VersionId == current.SystemDescriptionVersionId &&
            item.ChangeId == changeId);
        if (prior is not null)
            return prior.DecidedByMemberId == decidedByMemberId && prior.Rationale == rationale
                ? null
                : CommandFailure.StateConflict("The period-change disposition is already recorded.");

        var decision = new SystemDescriptionNoChangeDecisionView(current.SystemDescriptionVersionId,
            current.Version, change.ChangeId, rationale, decidedByMemberId,
            decidedByDisplay.Trim(), decidedAt);
        var ev = new SystemDescriptionNoChangeRecordedBody(_tenantId, _programId, Id,
            Revision + 1, decision, decidedAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? BindToExamination(long version, Uuid examinationId,
        string purpose, Uuid snapshotId, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset boundAt)
    {
        if (FindVersion(version) is not { Status: "approved" } approved)
            return CommandFailure.StateConflict(
                "Only an approved system description version can be bound to an examination.");
        if (ValidateActor(actorMemberId, actorDisplay, boundAt) is { } actorFailure)
            return actorFailure;
        if (examinationId != approved.Scope.ExaminationId || snapshotId == Uuid.Empty ||
            purpose is not ("type_i_baseline" or "type_ii_close") ||
            purpose == "type_i_baseline" && approved.Scope.Stage != "type_i" ||
            purpose == "type_ii_close" && approved.Scope.Stage != "type_ii")
            return Invalid("The system description binding must match its approved examination snapshot.");
        if (purpose == "type_i_baseline" && approved.Scope.TypeIBaselineId != snapshotId ||
            purpose == "type_ii_close" && approved.Scope.CloseSnapshotId != snapshotId)
            return CommandFailure.StateConflict(
                "The binding snapshot does not match the exact snapshot in the approved version.");
        var existing = _bindings.FirstOrDefault(item => item.VersionId == approved.SystemDescriptionVersionId &&
            item.Purpose == purpose);
        if (existing is not null)
            return existing.SnapshotId == snapshotId && existing.ExaminationId == examinationId
                ? null
                : CommandFailure.StateConflict(
                    "An approved description version cannot be rebound to different examination material.");

        var binding = new SystemDescriptionBindingView(approved.SystemDescriptionVersionId,
            approved.Version, examinationId, purpose, snapshotId, actorMemberId,
            actorDisplay.Trim(), boundAt);
        var ev = new SystemDescriptionBoundBody(_tenantId, _programId, Id, Revision + 1,
            binding, boundAt);
        RaiseEvent(ev);
        return null;
    }

    void Apply(SystemDescriptionDraftCreatedBody ev)
    {
        if (Revision != 0 || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.SystemDescriptionId != Id || ev.AggregateRevision != 1 ||
            ev.Version.Version != 1 || ev.Version.ParentVersionId is not null ||
            ev.Version.Status != "draft" || !ValidVersion(ev.Version, 1))
            throw new InvalidOperationException("The system description opening event is invalid.");
        _versions.Add(FreezeVersion(ev.Version));
        Revision = 1;
    }

    void Apply(SystemDescriptionDraftRevisedBody ev) => ApplyNewVersion(ev.TenantId, ev.ProgramId,
        ev.SystemDescriptionId, ev.AggregateRevision, ev.Version, expectedStatus: "draft");

    void Apply(SystemDescriptionSourcesBoundBody ev) => ApplyNewVersion(ev.TenantId, ev.ProgramId,
        ev.SystemDescriptionId, ev.AggregateRevision, ev.Version, expectedStatus: "draft");

    void Apply(SystemDescriptionSuccessorProposedBody ev) => ApplyNewVersion(ev.TenantId, ev.ProgramId,
        ev.SystemDescriptionId, ev.AggregateRevision, ev.Version, expectedStatus: "approved");

    void ApplyNewVersion(Uuid tenantId, Uuid programId, Uuid descriptionId,
        long aggregateRevision, SystemDescriptionVersionView version, string expectedStatus)
    {
        var current = CurrentVersion;
        if (current is null || tenantId != _tenantId || programId != _programId ||
            descriptionId != Id || aggregateRevision != Revision + 1 ||
            current.Status != expectedStatus || version.Version != current.Version + 1 ||
            version.ParentVersionId != current.SystemDescriptionVersionId ||
            version.Status != "draft" || !ValidVersion(version, version.Version) ||
            _versions.Any(item => item.SystemDescriptionVersionId == version.SystemDescriptionVersionId))
            throw new InvalidOperationException("A system description successor event is invalid.");
        if (current.Status == "draft")
            _versions[^1] = current with { Status = "superseded" };
        _versions.Add(FreezeVersion(version));
        Revision++;
    }

    void Apply(SystemDescriptionReviewedBody ev)
    {
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.SystemDescriptionId != Id ||
            ev.AggregateRevision != Revision + 1 || FindVersion(ev.Review.VersionId) is not
            { Status: "draft" } version || version.Version != ev.Review.Version ||
            ev.Review.ReviewerMemberId == version.AuthorMemberId ||
            _reviews.Any(item => item.ReviewId == ev.Review.ReviewId) ||
            ev.Review.Outcome is not ("approve" or "request_changes") ||
            !ValidActor(ev.Review.ReviewerMemberId, ev.Review.ReviewerDisplay, ev.OccurredAt) ||
            ev.Review.ReviewedAt != ev.OccurredAt)
            throw new InvalidOperationException("The system description review event is invalid.");
        _reviews.Add(ev.Review);
        Revision++;
    }

    void Apply(SystemDescriptionApprovedBody ev)
    {
        var approval = ev.Approval;
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.SystemDescriptionId != Id ||
            ev.AggregateRevision != Revision + 1 || FindVersion(approval.VersionId) is not
            { Status: "draft" } version || version.Version != approval.Version ||
            approval.ApproverMemberId == version.AuthorMemberId ||
            _reviews.LastOrDefault(item => item.VersionId == approval.VersionId)?.Outcome != "approve" ||
            !CanApprove(version) || !ValidActor(approval.ApproverMemberId,
                approval.ApproverDisplay, ev.OccurredAt) || approval.ApprovedAt != ev.OccurredAt ||
            approval.CloseSnapshotId != version.Scope.CloseSnapshotId ||
            _approvals.Any(item => item.VersionId == approval.VersionId))
            throw new InvalidOperationException("The system description approval event is invalid.");
        ReplaceVersion(version with { Status = "approved" });
        _approvals.Add(approval);
        Revision++;
    }

    void Apply(SystemDescriptionNoChangeRecordedBody ev)
    {
        var decision = ev.Decision;
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.SystemDescriptionId != Id ||
            ev.AggregateRevision != Revision + 1 || FindVersion(decision.VersionId) is not
            { Status: "draft", Scope.Stage: "type_ii" } version ||
            version.Version != decision.Version || version.AuthorMemberId == decision.DecidedByMemberId ||
            version.Content.PeriodChanges.FirstOrDefault(change => change.ChangeId == decision.ChangeId)
                is not { IsMaterial: false } ||
            !ValidActor(decision.DecidedByMemberId, decision.DecidedByDisplay, ev.OccurredAt) ||
            decision.DecidedAt != ev.OccurredAt ||
            _noChangeDecisions.Any(item => item.VersionId == decision.VersionId &&
                item.ChangeId == decision.ChangeId))
            throw new InvalidOperationException("The system description no-change event is invalid.");
        _noChangeDecisions.Add(decision);
        Revision++;
    }

    void Apply(SystemDescriptionBoundBody ev)
    {
        var binding = ev.Binding;
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.SystemDescriptionId != Id ||
            ev.AggregateRevision != Revision + 1 || FindVersion(binding.VersionId) is not
            { Status: "approved" } version || version.Version != binding.Version ||
            binding.ExaminationId != version.Scope.ExaminationId ||
            binding.Purpose == "type_i_baseline" &&
                (version.Scope.Stage != "type_i" || version.Scope.TypeIBaselineId != binding.SnapshotId) ||
            binding.Purpose == "type_ii_close" &&
                (version.Scope.Stage != "type_ii" || version.Scope.CloseSnapshotId != binding.SnapshotId) ||
            binding.Purpose is not ("type_i_baseline" or "type_ii_close") ||
            !ValidActor(binding.BoundByMemberId, binding.BoundByDisplay, ev.OccurredAt) ||
            binding.BoundAt != ev.OccurredAt ||
            _bindings.Any(item => item.VersionId == binding.VersionId && item.Purpose == binding.Purpose))
            throw new InvalidOperationException("The system description binding event is invalid.");
        _bindings.Add(binding);
        Revision++;
    }

    bool CanApprove(SystemDescriptionVersionView version) =>
        version.Content.Sections.All(static section => section.Status != "incomplete") &&
        version.Content.Sources.All(static source => source.Status == "current") &&
        (version.Scope.Stage != "type_ii" || version.Scope.CloseSnapshotId is not null) &&
        version.Content.PeriodChanges.All(change => change.IsMaterial
            ? version.Content.SignificantChangeIds.Contains(change.ChangeId)
            : _noChangeDecisions.Any(decision => decision.VersionId == version.SystemDescriptionVersionId &&
                decision.ChangeId == change.ChangeId));

    bool ValidVersion(SystemDescriptionVersionView version, long expectedVersion)
    {
        if (version.TenantId != _tenantId || version.ProgramId != _programId ||
            version.SystemDescriptionId != Id || version.SystemDescriptionVersionId == Uuid.Empty ||
            version.Version != expectedVersion || version.Status != "draft" ||
            !ValidActor(version.AuthorMemberId, version.AuthorDisplay, version.UpdatedAt) ||
            NormalizeScope(version.Scope) is not { } scope ||
            NormalizeContent(version.Content) is not { } content)
            return false;
        return scope == version.Scope && ContentHash(scope, content) == version.ContentSha256;
    }

    SystemDescriptionVersionView BuildVersion(Uuid versionId, Uuid? parentVersionId,
        SystemDescriptionScope scope, SystemDescriptionDraftContent content, string changeReason,
        Uuid authorMemberId, string authorDisplay, DateTimeOffset at, long version = 1)
    {
        var frozenContent = FreezeContent(content);
        return new SystemDescriptionVersionView(_tenantId, _programId, Id, versionId, version,
            parentVersionId, "draft", scope, frozenContent, changeReason, authorMemberId,
            authorDisplay, at, ContentHash(scope, frozenContent));
    }

    static SystemDescriptionVersionView FreezeVersion(SystemDescriptionVersionView version) => version with
    {
        Content = FreezeContent(version.Content),
    };

    static SystemDescriptionDraftContent FreezeContent(SystemDescriptionDraftContent content) => content with
    {
        Sections = Array.AsReadOnly(content.Sections.OrderBy(static section => section.SectionId,
            StringComparer.Ordinal).ToArray()),
        Sources = Array.AsReadOnly(content.Sources.OrderBy(static source => source.Kind,
            StringComparer.Ordinal).ThenBy(static source => source.RecordId.ToString(),
            StringComparer.Ordinal).ThenBy(static source => source.VersionId.ToString(), StringComparer.Ordinal)
            .ThenBy(static source => source.Revision).ToArray()),
        PeriodChanges = Array.AsReadOnly(content.PeriodChanges.OrderBy(static change =>
            change.ChangeId.ToString(), StringComparer.Ordinal).ToArray()),
        SignificantChangeIds = Array.AsReadOnly(content.SignificantChangeIds.OrderBy(static id =>
            id.ToString(), StringComparer.Ordinal).ToArray()),
    };

    void ReplaceVersion(SystemDescriptionVersionView version)
    {
        var index = _versions.FindIndex(item =>
            item.SystemDescriptionVersionId == version.SystemDescriptionVersionId);
        _versions[index] = version;
    }

    static SystemDescriptionScope? NormalizeScope(SystemDescriptionScope? scope)
    {
        if (scope is null || scope.ExaminationId == Uuid.Empty || scope.Stage is not ("type_i" or "type_ii"))
            return null;
        if (scope.Stage == "type_i" && (scope.TypeIBaselineId is null ||
                scope.TypeIBaselineId == Uuid.Empty || scope.TypeIIPeriodId is not null ||
                scope.CloseSnapshotId is not null))
            return null;
        if (scope.Stage == "type_ii" && (scope.TypeIIPeriodId is null ||
                scope.TypeIIPeriodId == Uuid.Empty || scope.TypeIBaselineId == Uuid.Empty ||
                scope.CloseSnapshotId == Uuid.Empty))
            return null;
        return scope;
    }

    static SystemDescriptionDraftContent? NormalizeContent(SystemDescriptionDraftContent? content)
    {
        if (content is null)
            return null;
        var title = content.Title?.Trim() ?? string.Empty;
        if (!BoundedText(title, MaximumTitleLength) || content.Sections is null ||
            content.Sections.Count != RequiredSectionIds.Count ||
            content.Sections.Count > MaximumSections || content.Sources is null ||
            content.PeriodChanges is null || content.SignificantChangeIds is null)
            return null;
        if (content.Sections.Any(section => section is null ||
                !RequiredSectionIds.Contains(section.SectionId) ||
                !BoundedText(section.Title?.Trim() ?? string.Empty, MaximumTitleLength) ||
                section.Content?.Length > MaximumSectionTextLength ||
                section.Status is not ("complete" or "incomplete" or "not_applicable") ||
                section.Status == "complete" && string.IsNullOrWhiteSpace(section.Content) ||
                section.Status == "not_applicable" &&
                    !BoundedText(section.NotApplicableRationale?.Trim() ?? string.Empty,
                        MaximumDecisionTextLength)) ||
            content.Sections.Select(static section => section.SectionId)
                .Distinct(StringComparer.Ordinal).Count() != RequiredSectionIds.Count)
            return null;
        if (content.Sources.Any(source => source is null || !ValidKind(source.Kind) ||
                source.RecordId == Uuid.Empty || source.VersionId == Uuid.Empty || source.Revision <= 0 ||
                !ValidDigest(source.ContentSha256) ||
                source.Status is not ("current" or "stale" or "missing" or "contradictory")) ||
            content.Sources.Select(static source => (source.Kind, source.RecordId, source.VersionId, source.Revision)).Distinct().Count() !=
            content.Sources.Count)
            return null;
        if (content.PeriodChanges.Any(change => change is null || change.ChangeId == Uuid.Empty ||
                !ValidDigest(change.AssessmentSha256)) ||
            content.PeriodChanges.Select(static change => change.ChangeId).Distinct().Count() !=
            content.PeriodChanges.Count || content.SignificantChangeIds.Any(static id => id == Uuid.Empty) ||
            content.SignificantChangeIds.Distinct().Count() != content.SignificantChangeIds.Count)
            return null;
        var significantIds = content.SignificantChangeIds.ToHashSet();
        if (content.PeriodChanges.Any(change => change.IsMaterial != significantIds.Contains(change.ChangeId)))
            return null;

        return content with
        {
            Title = title,
            Sections = Array.AsReadOnly(content.Sections.Select(section => section with
            {
                Title = section.Title.Trim(),
                Content = section.Content?.Trim() ?? string.Empty,
                NotApplicableRationale = section.NotApplicableRationale?.Trim(),
            }).OrderBy(static section => section.SectionId, StringComparer.Ordinal).ToArray()),
            Sources = Array.AsReadOnly(content.Sources.OrderBy(static source => source.Kind,
                StringComparer.Ordinal).ThenBy(static source => source.RecordId.ToString(),
                StringComparer.Ordinal).ThenBy(static source => source.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(static source => source.Revision).ToArray()),
            PeriodChanges = Array.AsReadOnly(content.PeriodChanges.OrderBy(static change =>
                change.ChangeId.ToString(), StringComparer.Ordinal).ToArray()),
            SignificantChangeIds = Array.AsReadOnly(content.SignificantChangeIds.OrderBy(static id =>
                id.ToString(), StringComparer.Ordinal).ToArray()),
        };
    }

    static string ContentHash(SystemDescriptionScope scope, SystemDescriptionDraftContent? content) =>
        content is null ? string.Empty : SystemDescriptionContentIdentity.Digest(scope, content);

    static bool ValidActor(Uuid memberId, string display, DateTimeOffset at) =>
        memberId != Uuid.Empty && BoundedText(display?.Trim() ?? string.Empty,
            MaximumActorDisplayLength) && at != default;

    static CommandFailure? ValidateActor(Uuid memberId, string display, DateTimeOffset at) =>
        ValidActor(memberId, display, at)
            ? null
            : Invalid("An attributed system description decision requires a member, display name, and time.");

    static bool ValidKind(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value[0] is >= 'a' and <= 'z' && value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool BoundedText(string value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;

    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
}
