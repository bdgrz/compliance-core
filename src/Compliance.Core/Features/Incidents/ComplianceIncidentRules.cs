using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Owns the compliance-facing incident chronology, significance decision, impact review, and resolution.</summary>
public sealed class ComplianceIncidentRules
{
    const int MaximumReferences = 1000;
    const int MaximumTextLength = 4000;
    static readonly HashSet<string> IncidentKinds = new(StringComparer.Ordinal)
        { "security", "availability", "operational", "privacy", "other" };
    static readonly HashSet<string> Severities = new(StringComparer.Ordinal)
        { "low", "medium", "high", "critical" };
    static readonly HashSet<string> HandlingClasses = new(StringComparer.Ordinal)
        { "public", "internal", "confidential", "restricted" };
    static readonly HashSet<string> ImpactDomains = new(StringComparer.Ordinal)
        { "scope", "risk", "vendor", "control", "evidence", "system_description", "period", "other" };
    static readonly HashSet<string> RecordTypes = new(StringComparer.Ordinal)
        { "system_boundary", "service_commitment", "control", "application", "system_instance",
            "technology_component", "information_asset", "risk", "vendor", "evidence_expectation",
            "system_description_section", "other" };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    ComplianceIncidentView? _current;

    readonly Dictionary<Type, Action<object>> _reducers = [];
    readonly List<object> _applied = [];
    readonly List<object> _pending = [];
    bool _currentOnly;
    public Uuid Id { get; }
    public EventStreamAddress Stream { get; }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public IReadOnlyList<object> AppliedChanges => _applied.AsReadOnly();
    public IReadOnlyList<object> PendingChanges => _pending.AsReadOnly();
    public void ClearPendingChanges() => _pending.Clear();
    void On<T>(Action<T> reducer) where T : class => _reducers.Add(typeof(T), value => reducer((T)value));
    public void Apply(object value)
    {
        if (!_reducers.TryGetValue(value.GetType(), out var reduce))
            throw new InvalidOperationException("The incident rule mutation has another kind.");
        reduce(value);
        _applied.Add(value);
    }
    internal void RaiseEvent(object value) { Apply(value); _pending.Add(value); }
    internal static ComplianceIncidentRules FromView(ComplianceIncidentView? view, Uuid tenant, Uuid program, Uuid id, bool currentOnly = false)
    {
        var rules = new ComplianceIncidentRules(tenant, program, id) { _currentOnly = currentOnly };
        if (view is not null)
            rules._current = Freeze(view);
        return rules;
    }

    public long Revision => _current?.Revision ?? 0;
    public ComplianceIncidentView? Current => _current;

    public ComplianceIncidentRules(Uuid tenantId, Uuid programId, Uuid incidentId)
    {
        Id = incidentId;
        Stream = new EventStreamAddress(tenantId.ToString(), ComplianceIncident.Area, incidentId.ToString());
        _tenantId = tenantId;
        _programId = programId;
        On<ComplianceIncidentRecordedBody>(Apply);
        On<ComplianceIncidentRevisedBody>(Apply);
        On<ComplianceIncidentImpactAssessedBody>(Apply);
        On<ComplianceIncidentSystemDescriptionDecisionRecordedBody>(Apply);
        On<IncidentResolutionRecordedBody>(Apply);
    }

    public CommandFailure? Record(ComplianceIncidentContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId, actorDisplay, recordedAt)
                ? null
                : CommandFailure.StateConflict("The compliance incident already exists with different content.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, recordedAt))
            return Invalid("A compliance incident requires tenant, program, incident, and attributed recorder identities.");
        if (NormalizeContent(content) is not { } normalized || recordedAt < normalized.DetectedAt)
            return Invalid("An incident requires source chronology, owner, affected systems, severity, classification, and evidence.");
        var version = NewVersion(normalized, 1, actorMemberId, actorDisplay.Trim(), recordedAt, "");
        var state = new ComplianceIncidentView(_tenantId, _programId, Id, "recorded", 1, 1,
            version, [version], [], [], null, actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(state, actorMemberId, recordedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ComplianceIncidentRecordedBody(tenant, subject, revision, actor, snapshot, at),
            "record", creating: true);
    }

    public CommandFailure? Revise(long expectedRevision, ComplianceIncidentContent content, string changeReason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (PlanRevise(expectedRevision, content, changeReason, actorMemberId, actorDisplay, at, out var state) is { } failure)
            return failure;
        return Emit(state!, actorMemberId, at, static (tenant, subject, revision, actor, snapshot, time) =>
            new ComplianceIncidentRevisedBody(tenant, subject, revision, actor, snapshot, time), "revise");
    }

    CommandFailure? PlanRevise(long expectedRevision, ComplianceIncidentContent content, string changeReason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt, out ComplianceIncidentView? state)
    {
        state = null;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The compliance incident was not found.");
        changeReason = Clean(changeReason);
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "resolved")
            return CommandFailure.StateConflict("A resolved incident cannot be revised.");
        if (!ValidActor(actorMemberId, actorDisplay, revisedAt) || revisedAt < current.UpdatedAt ||
            !Bounded(changeReason, 1, MaximumTextLength) || NormalizeContent(content) is not { } normalized)
            return Invalid("An incident correction requires complete facts, an attributed actor, and a reason.");
        if (normalized.OccurredAt != current.Current!.Content.OccurredAt ||
            normalized.DetectedAt != current.Current.Content.DetectedAt)
            return CommandFailure.StateConflict("Incident occurrence and detection times are immutable; record a correction in the chronology.");
        if (Digest(normalized) == current.Current.ContentSha256)
            return CommandFailure.StateConflict("A correction must change incident content.");
        var version = NewVersion(normalized, current.CurrentVersionNumber + 1,
            actorMemberId, actorDisplay.Trim(), revisedAt, changeReason);
        state = current with
        {
            Status = "recorded",
            Revision = Revision + 1,
            CurrentVersionNumber = version.Version,
            Current = version,
            Versions = Append(current.Versions, version),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = revisedAt,
        };
        return null;
    }

    public CommandFailure? Assess(long expectedRevision, ComplianceIncidentImpactAssessmentContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (PlanAssess(expectedRevision, content, actorMemberId, actorDisplay, at, out var state) is { } failure)
            return failure;
        return Emit(state!, actorMemberId, at, static (tenant, subject, revision, actor, snapshot, time) =>
            new ComplianceIncidentImpactAssessedBody(tenant, subject, revision, actor, snapshot, time), "assessment");
    }

    CommandFailure? PlanAssess(long expectedRevision, ComplianceIncidentImpactAssessmentContent content,
        Uuid assessorMemberId, string assessorDisplay, DateTimeOffset assessedAt, out ComplianceIncidentView? state)
    {
        state = null;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The compliance incident was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "resolved")
            return CommandFailure.StateConflict("A resolved incident cannot receive a new impact assessment.");
        if (!ValidActor(assessorMemberId, assessorDisplay, assessedAt) || assessedAt < current.UpdatedAt ||
            NormalizeAssessment(content) is not { } normalized)
            return Invalid("An incident assessment requires reasoned, reviewed scope and control impacts.");
        var source = current.Current!.Content;
        if (normalized.CustomerDataAffected != source.CustomerDataAffected ||
            normalized.AvailabilitySlaAffected != source.AvailabilitySlaAffected)
            return Invalid("The assessment must use the incident's recorded customer-data and SLA facts.");
        var requiredSignificance = source.IncidentKind == "security" && source.CustomerDataAffected ||
                                   source.AvailabilitySlaAffected;
        if (normalized.Significance != (requiredSignificance ? "significant" : "not_significant"))
            return Invalid("Incident significance must follow the accepted customer-data and availability-SLA rule.");
        if (normalized.Significance == "significant" && normalized.AffectedRecords.Count == 0)
            return Invalid("A significant incident must identify its affected records.");
        if (normalized.RequiresSystemDescriptionReview &&
            (normalized.Significance != "significant" || !normalized.ImpactDomains.Contains("system_description")))
            return Invalid("A system-description review requires an explicit significant incident impact.");
        if (current.Assessments.Any(item => item.ContentVersion == current.CurrentVersionNumber))
            return CommandFailure.StateConflict("An incident version has one immutable impact assessment.");
        var assessment = new ComplianceIncidentImpactAssessmentView(current.CurrentVersionNumber,
            normalized.Significance, normalized.Rationale, normalized.ScopeImpactReviewed,
            normalized.ControlImpactReviewed, normalized.RiskImpactReviewed,
            normalized.VendorImpactReviewed, normalized.ImpactDomains, normalized.AffectedRecords,
            normalized.FollowUpWorkIds, normalized.EvidenceArtifactIds,
            normalized.RequiresSystemDescriptionReview, normalized.CustomerDataAffected,
            normalized.AvailabilitySlaAffected, assessorMemberId,
            assessorDisplay.Trim(), assessedAt);
        state = current with
        {
            Status = normalized.RequiresSystemDescriptionReview ? "needs_review" : "assessed",
            Revision = Revision + 1,
            Assessments = Append(current.Assessments, assessment),
            ActorMemberId = assessorMemberId,
            ActorDisplay = assessorDisplay.Trim(),
            UpdatedAt = assessedAt,
        };
        return null;
    }

    public CommandFailure? RecordSystemDescriptionDecision(string decision, Uuid decisionId, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (PlanDecision(decision, decisionId, rationale, actorMemberId, actorDisplay, at, out var state) is { } failure)
            return failure;
        return Emit(state!, actorMemberId, at, static (tenant, subject, revision, actor, snapshot, time) =>
            new ComplianceIncidentSystemDescriptionDecisionRecordedBody(tenant, subject, revision, actor, snapshot, time), "description_decision");
    }

    CommandFailure? PlanDecision(string decision, Uuid decisionId, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset decidedAt, out ComplianceIncidentView? state)
    {
        state = null;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The compliance incident was not found.");
        decision = Clean(decision).ToLowerInvariant();
        rationale = Clean(rationale);
        var assessment = current.CurrentAssessment;
        if (assessment is null || !assessment.RequiresSystemDescriptionReview || current.Status != "needs_review")
            return CommandFailure.StateConflict("This incident has no unresolved system-description review.");
        if (!ValidActor(actorMemberId, actorDisplay, decidedAt) || decidedAt < assessment.AssessedAt ||
            decisionId == Uuid.Empty || !Bounded(rationale, 1, MaximumTextLength) ||
            decision is not ("approved_successor" or "no_change"))
            return Invalid("An incident description disposition requires an exact approved successor or recorded no-change decision.");
        if (actorMemberId == assessment.AssessedByMemberId)
            return CommandFailure.ActorProhibited("The incident assessor cannot independently decide the description review.");
        var disposition = new ComplianceIncidentSystemDescriptionDecisionView(
            current.CurrentVersionNumber, decision, decisionId, rationale,
            actorMemberId, actorDisplay.Trim(), decidedAt);
        state = current with
        {
            Status = "assessed",
            Revision = Revision + 1,
            SystemDescriptionDecisions = Append(current.SystemDescriptionDecisions, disposition),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = decidedAt,
        };
        return null;
    }

    public CommandFailure? Resolve(long expectedRevision, VerifiedIncidentResolution resolution,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (PlanResolve(expectedRevision, resolution, actorMemberId, actorDisplay, at, out var state) is { } failure)
            return failure;
        return Emit(state!, actorMemberId, at, static (tenant, subject, revision, actor, snapshot, time) =>
            new IncidentResolutionRecordedBody(tenant, subject, revision, actor, snapshot, time), "resolution");
    }

    CommandFailure? PlanResolve(long expectedRevision, VerifiedIncidentResolution resolution, Uuid resolverMemberId,
        string resolverDisplay, DateTimeOffset recordedAt, out ComplianceIncidentView? state)
    {
        state = null;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The compliance incident was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        var assessment = current.CurrentAssessment;
        if (assessment is null || current.Status != "assessed")
            return CommandFailure.StateConflict("The current incident version must be assessed and have no unresolved description review before resolution.");
        if (!ValidActor(resolverMemberId, resolverDisplay, recordedAt) || recordedAt < current.UpdatedAt ||
            NormalizeResolution(resolution) is not { } normalized)
            return Invalid("Incident resolution requires an attributed resolver, rationale, and governed evidence.");
        if (assessment.RequiresSystemDescriptionReview && current.CurrentSystemDescriptionDecision is null)
            return CommandFailure.StateConflict("Incident resolution is blocked until the system-description review is decided.");
        if (!assessment.FollowUpWorkIds.OrderBy(IdKey, StringComparer.Ordinal)
                .SequenceEqual(normalized.Completions.Select(static item => item.WorkItemId)))
            return CommandFailure.StateConflict("Every follow-up work item must be completed exactly once before incident resolution.");
        if (normalized.ResolvedAt < assessment.AssessedAt || normalized.ResolvedAt > recordedAt)
            return Invalid("The incident resolution time must follow assessment and not exceed its recording time.");
        var view = new ComplianceIncidentResolutionView(current.CurrentVersionNumber,
            normalized.Resolution, normalized.ResolvedAt, normalized.EvidenceArtifactIds,
            Freeze(normalized.Completions.Select(static item => item.WorkItemId).ToArray()),
            normalized.Completions, resolverMemberId, resolverDisplay.Trim());
        state = current with
        {
            Status = "resolved",
            Revision = Revision + 1,
            Resolution = view,
            ActorMemberId = resolverMemberId,
            ActorDisplay = resolverDisplay.Trim(),
            UpdatedAt = recordedAt,
        };
        return null;
    }

    void Apply(ComplianceIncidentRecordedBody ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "record");
    void Apply(ComplianceIncidentRevisedBody ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "revise");
    void Apply(ComplianceIncidentImpactAssessedBody ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "assessment");
    void Apply(ComplianceIncidentSystemDescriptionDecisionRecordedBody ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "description_decision");
    void Apply(IncidentResolutionRecordedBody ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "resolution");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        ComplianceIncidentView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.ComplianceIncidentId != Id ||
            state.Revision != revision || state.UpdatedAt != occurredAt || occurredAt == default ||
            revision != Revision + 1 || creating != (_current is null) || actorMemberId == Uuid.Empty ||
            state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "record" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The compliance-incident event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(ComplianceIncidentView state, Uuid actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, ComplianceIncidentView, DateTimeOffset, object> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null ||
                !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The compliance-incident transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? ValidateState(ComplianceIncidentView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId || state.ComplianceIncidentId != Id ||
            state.Revision <= 0 || state.Versions is null || state.Versions.Count < 1 ||
            state.Current is null || (!_currentOnly && state.CurrentVersionNumber != state.Versions.Count) ||
            state.Current.Version != state.CurrentVersionNumber || state.Assessments is null ||
            state.SystemDescriptionDecisions is null ||
            state.Versions.Where((version, index) => version.Version != (_currentOnly ? state.Versions[0].Version : 1) + index ||
                NormalizeContent(version.Content) is not { } normalized || Digest(normalized) != version.ContentSha256 ||
                version.RecordedByMemberId == Uuid.Empty || !Bounded(version.RecordedByDisplay, 1, 200) ||
                version.RecordedAt < version.Content.DetectedAt ||
                !Bounded(version.ChangeReason, 0, MaximumTextLength)).Any() ||
            !SameVersion(state.Current, state.Versions[^1]) ||
            state.Assessments.Count > state.Versions.Count ||
            state.Assessments.Select(item => item.ContentVersion).Distinct().Count() != state.Assessments.Count ||
            state.Assessments.Any(item => !ValidAssessment(item, state.Versions)) ||
            state.SystemDescriptionDecisions.Any(item => item.ContentVersion < 1 ||
                item.ContentVersion > state.CurrentVersionNumber || item.Decision is not ("approved_successor" or "no_change") ||
                item.DecisionId == Uuid.Empty || !Bounded(item.Rationale, 1, MaximumTextLength) ||
                !ValidActor(item.DecidedByMemberId, item.DecidedByDisplay, item.DecidedAt) ||
                AssessmentForVersion(state.Assessments, item.ContentVersion) is not { } assessment ||
                assessment.AssessedByMemberId == item.DecidedByMemberId ||
                !assessment.RequiresSystemDescriptionReview) ||
            state.SystemDescriptionDecisions.Select(item => item.ContentVersion).Distinct().Count() != state.SystemDescriptionDecisions.Count ||
            !ValidResolution(state) || !ValidStatus(state) ||
            !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt))
            return Invalid("The compliance-incident event contains an invalid state snapshot.");
        return null;
    }

    static bool ValidTransition(ComplianceIncidentView prior, ComplianceIncidentView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.ComplianceIncidentId != prior.ComplianceIncidentId ||
            !Prefix(prior.Assessments, next.Assessments) ||
            !Prefix(prior.SystemDescriptionDecisions, next.SystemDescriptionDecisions) ||
            PrefixDifferenceResolution(prior.Resolution, next.Resolution, transition))
            return false;
        return transition switch
        {
            "revise" => prior.Status != "resolved" && next.Status == "recorded" &&
                next.Versions.Count == prior.Versions.Count + 1 && SameVersions(prior.Versions,
                    next.Versions.Take(prior.Versions.Count).ToArray()) &&
                next.Current!.RecordedByMemberId == actor && next.Current.RecordedAt == next.UpdatedAt &&
                next.Assessments.Count == prior.Assessments.Count &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count &&
                prior.Resolution is null && next.Resolution is null,
            "assessment" => next.Status is "assessed" or "needs_review" &&
                SameVersions(prior.Versions, next.Versions) &&
                next.Assessments.Count == prior.Assessments.Count + 1 &&
                next.Assessments[^1].ContentVersion == next.CurrentVersionNumber &&
                next.Assessments[^1].AssessedByMemberId == actor &&
                next.Assessments[^1].AssessedAt == next.UpdatedAt &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count &&
                prior.Resolution is null && next.Resolution is null,
            "description_decision" => prior.Status == "needs_review" && next.Status == "assessed" &&
                SameVersions(prior.Versions, next.Versions) && SameAssessments(prior.Assessments, next.Assessments) &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count + 1 &&
                next.SystemDescriptionDecisions[^1].ContentVersion == next.CurrentVersionNumber &&
                next.SystemDescriptionDecisions[^1].DecidedByMemberId == actor &&
                next.SystemDescriptionDecisions[^1].DecidedAt == next.UpdatedAt && next.Resolution is null,
            "resolution" => prior.Status == "assessed" && next.Status == "resolved" &&
                SameVersions(prior.Versions, next.Versions) && SameAssessments(prior.Assessments, next.Assessments) &&
                Prefix(prior.SystemDescriptionDecisions, next.SystemDescriptionDecisions) &&
                prior.Resolution is null && next.Resolution is { } resolution &&
                resolution.ResolvedByMemberId == actor && resolution.ResolvedAt <= next.UpdatedAt,
            _ => false,
        };
    }

    static bool PrefixDifferenceResolution(ComplianceIncidentResolutionView? prior,
        ComplianceIncidentResolutionView? next, string transition) => transition == "resolution"
        ? prior is not null || next is null
        : prior != next;

    static bool ValidAssessment(ComplianceIncidentImpactAssessmentView item,
        IReadOnlyList<ComplianceIncidentVersionView> versions)
    {
        if (item.ContentVersion < 1 || versions.All(version => version.Version != item.ContentVersion) ||
            !Bounded(item.Rationale, 1, MaximumTextLength) ||
            !item.ScopeImpactReviewed || !item.ControlImpactReviewed || !item.RiskImpactReviewed ||
            !item.VendorImpactReviewed || item.ImpactDomains is null || item.ImpactDomains.Count < 5 ||
            item.ImpactDomains.Any(domain => !ImpactDomains.Contains(domain)) ||
            !item.ImpactDomains.Contains("scope") || !item.ImpactDomains.Contains("control") ||
            !item.ImpactDomains.Contains("risk") || !item.ImpactDomains.Contains("vendor") ||
            !item.ImpactDomains.Contains("evidence") || item.AffectedRecords is null ||
            item.AffectedRecords.Count > MaximumReferences || item.AffectedRecords.Any(record => !ValidAffectedRecord(record)) ||
            item.FollowUpWorkIds is null || !ValidIds(item.FollowUpWorkIds) ||
            item.EvidenceArtifactIds is null || !ValidIds(item.EvidenceArtifactIds) ||
            !ValidActor(item.AssessedByMemberId, item.AssessedByDisplay, item.AssessedAt))
            return false;
        var content = versions.First(version => version.Version == item.ContentVersion).Content;
        var significant = content.IncidentKind == "security" && content.CustomerDataAffected ||
                          content.AvailabilitySlaAffected;
        return item.Significance == (significant ? "significant" : "not_significant") &&
            item.Significance is "significant" or "not_significant" &&
            item.CustomerDataAffected == content.CustomerDataAffected &&
            item.AvailabilitySlaAffected == content.AvailabilitySlaAffected &&
            (item.Significance != "significant" || item.AffectedRecords.Count > 0) &&
            (!item.RequiresSystemDescriptionReview || item.Significance == "significant" &&
                item.ImpactDomains.Contains("system_description"));
    }

    static bool ValidResolution(ComplianceIncidentView state)
    {
        if (state.Resolution is null)
            return state.Status != "resolved";
        var resolution = state.Resolution;
        var assessment = state.Assessments.FirstOrDefault(item => item.ContentVersion == resolution.ContentVersion);
        return assessment is not null && resolution.ContentVersion == state.CurrentVersionNumber &&
            Bounded(resolution.Resolution, 1, MaximumTextLength) && resolution.ResolvedAt >= assessment.AssessedAt &&
            resolution.EvidenceArtifactIds is not null && resolution.EvidenceArtifactIds.Count > 0 &&
            ValidIds(resolution.EvidenceArtifactIds) && resolution.ResolvedFollowUpWorkIds is not null &&
            ValidIds(resolution.ResolvedFollowUpWorkIds) && resolution.FollowUpCompletions is not null &&
            assessment.FollowUpWorkIds.OrderBy(IdKey, StringComparer.Ordinal)
                .SequenceEqual(resolution.ResolvedFollowUpWorkIds) &&
            resolution.FollowUpCompletions.Select(static item => item.WorkItemId)
                .SequenceEqual(resolution.ResolvedFollowUpWorkIds) &&
            resolution.FollowUpCompletions.All(item => ValidCompletion(item, state.TenantId)) &&
            ValidActor(resolution.ResolvedByMemberId, resolution.ResolvedByDisplay, state.UpdatedAt);
    }

    static bool ValidCompletion(ComplianceIncidentFollowUpCompletionView item, Uuid tenantId) =>
        item is { Proof: { Source: { } source } proof } && item.WorkItemId != Uuid.Empty &&
        source.Realm == tenantId.ToString() && source.Area == CorrectiveAction.Area &&
        proof.EventKind == EventKind.Of(typeof(CorrectiveActionCompletedV2)) && proof.EventId != Uuid.Empty;

    static bool ValidStatus(ComplianceIncidentView state)
    {
        var assessment = state.Assessments.Count == 0 ? null : state.Assessments[^1];
        var currentAssessment = assessment?.ContentVersion == state.CurrentVersionNumber ? assessment : null;
        var currentDecision = state.SystemDescriptionDecisions.Count == 0 ? null : state.SystemDescriptionDecisions[^1];
        var hasCurrentDecision = currentDecision?.ContentVersion == state.CurrentVersionNumber;
        var status = state.Resolution is not null ? "resolved" : currentAssessment is null ? "recorded" :
            currentAssessment.RequiresSystemDescriptionReview && !hasCurrentDecision ? "needs_review" : "assessed";
        return state.Status == status;
    }

    static bool ValidInitialState(ComplianceIncidentView state) => state.Status == "recorded" &&
        state.Revision == 1 && state.CurrentVersionNumber == 1 && state.Assessments.Count == 0 &&
        state.SystemDescriptionDecisions.Count == 0 && state.Resolution is null;

    static ComplianceIncidentContent? NormalizeContent(ComplianceIncidentContent? content)
    {
        if (content is null || !Bounded(content.Title, 1, 300) ||
            !Bounded(content.Description, 1, MaximumTextLength) ||
            !IncidentKinds.Contains(Clean(content.IncidentKind).ToLowerInvariant()) ||
            !Severities.Contains(Clean(content.Severity).ToLowerInvariant()) ||
            content.OccurredAt == default || content.DetectedAt < content.OccurredAt ||
            content.OwnerMemberId == Uuid.Empty || content.AffectedSystemIds is null ||
            content.AffectedSystemIds.Count is < 1 or > MaximumReferences || !ValidIds(content.AffectedSystemIds) ||
            content.EvidenceArtifactIds is null || !ValidIds(content.EvidenceArtifactIds))
            return null;
        var sourceSystem = Clean(content.SourceSystem);
        var sourceIdentifier = Clean(content.SourceIdentifier);
        var sourceUrl = Clean(content.SourceUrl);
        if ((sourceSystem.Length == 0) != (sourceIdentifier.Length == 0) ||
            sourceSystem.Length > 200 || sourceIdentifier.Length > 300 || sourceUrl.Length > 2000 ||
            sourceUrl.Length > 0 && !ValidUrl(sourceUrl))
            return null;
        var handlingClass = Clean(content.HandlingClass);
        if (handlingClass.Length == 0)
            handlingClass = "confidential";
        if (!HandlingClasses.Contains(handlingClass) || content.EarlierRiskIncidentReference is { } riskReference &&
            !ValidRiskReference(riskReference))
            return null;
        return content with
        {
            SourceSystem = sourceSystem.Length == 0 ? null : sourceSystem,
            SourceIdentifier = sourceIdentifier.Length == 0 ? null : sourceIdentifier,
            SourceUrl = sourceUrl.Length == 0 ? null : sourceUrl,
            Title = content.Title.Trim(),
            Description = content.Description.Trim(),
            IncidentKind = Clean(content.IncidentKind).ToLowerInvariant(),
            Severity = Clean(content.Severity).ToLowerInvariant(),
            HandlingClass = handlingClass,
            AffectedSystemIds = Freeze(content.AffectedSystemIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            EvidenceArtifactIds = Freeze(content.EvidenceArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            EarlierRiskIncidentReference = content.EarlierRiskIncidentReference is { } reference
                ? reference with
                {
                    SourceSystem = reference.SourceSystem.Trim(),
                    SourceIdentifier = reference.SourceIdentifier.Trim(),
                    SourceUrl = reference.SourceUrl.Trim(),
                    Summary = reference.Summary.Trim(),
                } : null,
        };
    }

    static ComplianceIncidentImpactAssessmentContent? NormalizeAssessment(
        ComplianceIncidentImpactAssessmentContent? content)
    {
        if (content is null || content.Significance is not ("significant" or "not_significant") ||
            !Bounded(content.Rationale, 1, MaximumTextLength) || content.ImpactDomains is null ||
            content.ImpactDomains.Count is < 5 or > 16 || content.ImpactDomains.Any(domain => !ImpactDomains.Contains(Clean(domain))) ||
            content.ImpactDomains.Select(Clean).Distinct(StringComparer.Ordinal).Count() != content.ImpactDomains.Count ||
            content.AffectedRecords is null || content.AffectedRecords.Count > MaximumReferences ||
            content.AffectedRecords.Any(item => !ValidAffectedRecord(item)) ||
            content.AffectedRecords.Select(item => (item.RecordType, item.RecordId, item.VersionId)).Distinct().Count() != content.AffectedRecords.Count ||
            content.FollowUpWorkIds is null || !ValidIds(content.FollowUpWorkIds) ||
            content.EvidenceArtifactIds is null || !ValidIds(content.EvidenceArtifactIds) ||
            !content.ScopeImpactReviewed || !content.ControlImpactReviewed ||
            !content.RiskImpactReviewed || !content.VendorImpactReviewed ||
            !content.ImpactDomains.Contains("scope") || !content.ImpactDomains.Contains("control") ||
            !content.ImpactDomains.Contains("risk") || !content.ImpactDomains.Contains("vendor") ||
            !content.ImpactDomains.Contains("evidence"))
            return null;
        return content with
        {
            Significance = content.Significance.Trim(),
            Rationale = content.Rationale.Trim(),
            ImpactDomains = Freeze(content.ImpactDomains.Select(Clean).Order(StringComparer.Ordinal).ToArray()),
            AffectedRecords = Freeze(content.AffectedRecords.OrderBy(item => item.RecordType, StringComparer.Ordinal)
                .ThenBy(item => IdKey(item.RecordId), StringComparer.Ordinal).ToArray()),
            FollowUpWorkIds = Freeze(content.FollowUpWorkIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            EvidenceArtifactIds = Freeze(content.EvidenceArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
        };
    }

    static VerifiedIncidentResolution? NormalizeResolution(VerifiedIncidentResolution? content)
    {
        if (content is null || !Bounded(content.Resolution, 1, MaximumTextLength) || content.ResolvedAt == default ||
            content.EvidenceArtifactIds is null || content.EvidenceArtifactIds.Count == 0 ||
            !ValidIds(content.EvidenceArtifactIds) || content.Completions is null ||
            content.Completions.Any(item => item is null || item.WorkItemId == Uuid.Empty) ||
            !ValidIds(content.Completions.Select(static item => item.WorkItemId).ToArray()))
            return null;
        return content with
        {
            Resolution = content.Resolution.Trim(),
            EvidenceArtifactIds = Freeze(content.EvidenceArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            Completions = Freeze(content.Completions.OrderBy(static item => IdKey(item.WorkItemId),
                StringComparer.Ordinal).ToArray()),
        };
    }

    static bool ValidAffectedRecord(ComplianceIncidentAffectedRecordReference? item) => item is not null &&
        RecordTypes.Contains(item.RecordType) && item.RecordId != Uuid.Empty && item.VersionId != Uuid.Empty && item.Version > 0;
    static bool ValidRiskReference(RiskIncidentReference reference) =>
        Bounded(reference.SourceSystem, 1, 200) && Bounded(reference.SourceIdentifier, 1, 300) &&
        ValidUrl(reference.SourceUrl) && reference.OccurredAt != default &&
        Bounded(reference.Summary, 1, 1000);
    static ComplianceIncidentImpactAssessmentView? AssessmentForVersion(
        IReadOnlyList<ComplianceIncidentImpactAssessmentView> assessments, int version)
    {
        foreach (var assessment in assessments)
            if (assessment.ContentVersion == version)
                return assessment;
        return null;
    }
    static ComplianceIncidentVersionView NewVersion(ComplianceIncidentContent content, int version,
        Uuid actor, string display, DateTimeOffset at, string reason) =>
        new(version, content, Digest(content), actor, display, at, reason);
    static bool SameInitial(ComplianceIncidentView state, ComplianceIncidentContent? content,
        Uuid actor, string display, DateTimeOffset at) => NormalizeContent(content) is { } normalized &&
        state.Current is { } current && current.ContentSha256 == Digest(normalized) &&
        current.RecordedByMemberId == actor && current.RecordedByDisplay == display.Trim() && current.RecordedAt == at;
    static string AssessmentDigest(ComplianceIncidentImpactAssessmentView item) => Digest(new
    {
        item.Significance,
        item.Rationale,
        item.ScopeImpactReviewed,
        item.ControlImpactReviewed,
        item.RiskImpactReviewed,
        item.VendorImpactReviewed,
        item.ImpactDomains,
        item.AffectedRecords,
        item.FollowUpWorkIds,
        item.EvidenceArtifactIds,
        item.RequiresSystemDescriptionReview,
        item.CustomerDataAffected,
        item.AvailabilitySlaAffected,
    });

    /// <summary>Returns the canonical digest of the assessed facts, excluding actor attribution.</summary>
    public static string AssessmentSha256(ComplianceIncidentImpactAssessmentView item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return AssessmentDigest(item);
    }
    static bool SameVersion(ComplianceIncidentVersionView left, ComplianceIncidentVersionView right) =>
        left.Version == right.Version && left.ContentSha256 == right.ContentSha256 &&
        left.RecordedByMemberId == right.RecordedByMemberId && left.RecordedByDisplay == right.RecordedByDisplay &&
        left.RecordedAt == right.RecordedAt && left.ChangeReason == right.ChangeReason;
    static bool SameVersions(IReadOnlyList<ComplianceIncidentVersionView> left,
        IReadOnlyList<ComplianceIncidentVersionView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => SameVersion(pair.First, pair.Second));
    static bool SameAssessments(IReadOnlyList<ComplianceIncidentImpactAssessmentView> left,
        IReadOnlyList<ComplianceIncidentImpactAssessmentView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => Digest(pair.First) == Digest(pair.Second));
    static bool Prefix<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) => prior.Count <= next.Count &&
        prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));
    static bool ValidIds(IReadOnlyList<Uuid> ids) => ids.Count <= MaximumReferences &&
        ids.All(static id => id != Uuid.Empty) && ids.Distinct().Count() == ids.Count;
    static Uuid[]? NormalizeIds(IReadOnlyList<Uuid>? ids) => ids is not null && ValidIds(ids)
        ? ids.OrderBy(IdKey, StringComparer.Ordinal).ToArray() : null;
    static bool ValidUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "https" or "http";
    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static string IdKey(Uuid id) => id.ToString();
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty &&
        Bounded(display, 1, 200) && at != default;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("compliance incident", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) => Freeze(values.Append(value).ToArray());
    static ComplianceIncidentView Freeze(ComplianceIncidentView state) => state with
    {
        Current = state.Versions[^1],
        Versions = Freeze(state.Versions.ToArray()),
        Assessments = Freeze(state.Assessments.ToArray()),
        SystemDescriptionDecisions = Freeze(state.SystemDescriptionDecisions.ToArray()),
        Resolution = state.Resolution is { } resolution ? resolution with
        {
            EvidenceArtifactIds = Freeze(resolution.EvidenceArtifactIds.ToArray()),
            ResolvedFollowUpWorkIds = Freeze(resolution.ResolvedFollowUpWorkIds.ToArray()),
            FollowUpCompletions = Freeze(resolution.FollowUpCompletions.ToArray()),
        } : null,
    };
}
