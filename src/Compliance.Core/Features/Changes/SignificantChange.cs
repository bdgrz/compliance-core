using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Owns a native significant-change fact, impact history, follow-up work, and description disposition.</summary>
public sealed class SignificantChange : Aggregate
{
    const int MaximumVersions = 32;
    const int MaximumReferences = 1000;
    const int MaximumEventBytes = 256 * 1024;
    const int MaximumTextLength = 4000;
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
    SignificantChangeView? _current;

    public long Revision => _current?.Revision ?? 0;
    public SignificantChangeView? Current => _current;

    public SignificantChange(Uuid tenantId, Uuid programId, Uuid changeId)
        : base(changeId, new EventStreamAddress(tenantId.ToString(), "significant-changes",
            changeId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<SignificantChangeRecorded>(Apply);
        On<SignificantChangeRevised>(Apply);
        On<SignificantChangeImpactAssessed>(Apply);
        On<ImpactReviewWorkLinked>(Apply);
        On<SignificantChangeSystemDescriptionDecisionRecorded>(Apply);
    }

    public CommandFailure? Record(SignificantChangeContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId, actorDisplay, recordedAt)
                ? null
                : CommandFailure.StateConflict("The significant change already exists with different content.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, recordedAt))
            return Invalid("A significant change requires tenant, program, record, and actor identities.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("A significant change requires source time, owner, affected systems, severity, and governed evidence references.");
        var version = NewVersion(normalized, 1, actorMemberId, actorDisplay.Trim(), recordedAt, "");
        var state = new SignificantChangeView(_tenantId, _programId, Id, "recorded", 1, 1,
            version, [version], [], [], [], actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(state, actorMemberId, recordedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new SignificantChangeRecorded(tenant, subject, revision, actor, snapshot, at),
            "record", creating: true);
    }

    public CommandFailure? Revise(long expectedRevision, SignificantChangeContent content,
        string changeReason, Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The significant change was not found.");
        changeReason = Clean(changeReason);
        if (SameRevision(current, content, changeReason, actorMemberId, actorDisplay, revisedAt))
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "closed" || current.Versions.Count >= MaximumVersions)
            return CommandFailure.StateConflict("A closed change or exhausted history cannot be revised.");
        if (!ValidActor(actorMemberId, actorDisplay, revisedAt) || revisedAt < current.UpdatedAt ||
            !Bounded(changeReason, 1, MaximumTextLength) || NormalizeContent(content) is not { } normalized)
            return Invalid("A change correction requires complete content, an attributed actor, and a reason.");
        if (Digest(normalized) == current.Current!.ContentSha256)
            return CommandFailure.StateConflict("A correction must change the significant-change content.");
        var version = NewVersion(normalized, current.CurrentVersionNumber + 1,
            actorMemberId, actorDisplay.Trim(), revisedAt, changeReason);
        var state = current with
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
        return Emit(state, actorMemberId, revisedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new SignificantChangeRevised(tenant, subject, revision, actor, snapshot, at),
            "revise");
    }

    public CommandFailure? Assess(long expectedRevision, SignificantChangeImpactContent content,
        Uuid assessorMemberId, string assessorDisplay, DateTimeOffset assessedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The significant change was not found.");
        if (SameAssessment(current, content, assessorMemberId, assessorDisplay, assessedAt))
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "closed")
            return CommandFailure.StateConflict("A closed change cannot receive another impact assessment.");
        if (!ValidActor(assessorMemberId, assessorDisplay, assessedAt) || assessedAt < current.UpdatedAt ||
            NormalizeImpact(content) is not { } normalized)
            return Invalid("A change-impact assessment requires a supported significance, rationale, and exact affected records.");
        if (normalized.Significance != (HasSignificanceBasis(normalized) ? "significant" : "not_significant"))
            return Invalid("Significance must match the recorded boundary, commitment, key-control, and in-scope infrastructure impacts.");
        if (normalized.Significance == "significant" && normalized.AffectedRecords.Count == 0)
            return Invalid("A significant change must identify its affected records.");
        if (normalized.RequiresSystemDescriptionReview &&
            (normalized.Significance != "significant" || !normalized.ImpactDomains.Contains("system_description")))
            return Invalid("A description review is required only for a significant change with an explicit description impact.");
        if (current.Assessments.Any(item => item.ContentVersion == current.CurrentVersionNumber))
            return CommandFailure.StateConflict("A change version has one immutable significance assessment.");

        var assessment = new SignificantChangeImpactView(current.CurrentVersionNumber,
            normalized.Significance, normalized.Rationale, normalized.ChangesSystemBoundary,
            normalized.ChangesServiceCommitment, normalized.ChangesKeyControl,
            normalized.ChangesInScopeInfrastructure, normalized.ImpactDomains,
            normalized.AffectedRecords, normalized.RelatedReviewWorkIds,
            normalized.RelatedCorrectiveActionIds, normalized.RelatedExceptionIds,
            normalized.EvidenceArtifactIds,
            normalized.RequiresSystemDescriptionReview,
            normalized.RequiresSystemDescriptionReview ? "needs_review" : "not_required",
            assessorMemberId, assessorDisplay.Trim(), assessedAt);
        var state = current with
        {
            Status = normalized.RequiresSystemDescriptionReview ? "needs_review" : "assessed",
            Revision = Revision + 1,
            Assessments = Append(current.Assessments, assessment),
            ActorMemberId = assessorMemberId,
            ActorDisplay = assessorDisplay.Trim(),
            UpdatedAt = assessedAt,
        };
        return Emit(state, assessorMemberId, assessedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new SignificantChangeImpactAssessed(tenant, subject, revision, actor, snapshot, at),
            "assessment");
    }

    public CommandFailure? LinkReviewWork(long expectedRevision,
        IReadOnlyList<Uuid> workIds, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset linkedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The significant change was not found.");
        var assessment = current.CurrentAssessment;
        var normalizedIds = NormalizeIds(workIds);
        if (assessment is not null && normalizedIds is not null &&
            assessment.RelatedReviewWorkIds.Concat(assessment.RelatedCorrectiveActionIds)
                .OrderBy(IdKey, StringComparer.Ordinal).SequenceEqual(normalizedIds) &&
            assessment.RelatedReviewWorkIds.Concat(assessment.RelatedCorrectiveActionIds).All(id =>
                current.ReviewWorkLinks.Any(link => link.ContentVersion == current.CurrentVersionNumber &&
                    link.WorkId == id)))
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (assessment is null || normalizedIds is null || !ValidActor(actorMemberId, actorDisplay, linkedAt) ||
            linkedAt < assessment.AssessedAt)
            return Invalid("Review-work links require a current assessment and attributed work identities.");
        var expected = assessment.RelatedReviewWorkIds.Concat(assessment.RelatedCorrectiveActionIds)
            .OrderBy(IdKey, StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(normalizedIds))
            return CommandFailure.InvalidContent("Every expected review and corrective-action work item must be linked exactly once.");
        if (expected.Any(id => current.ReviewWorkLinks.Any(link =>
                link.ContentVersion == current.CurrentVersionNumber && link.WorkId == id)))
            return CommandFailure.StateConflict("A work item cannot be linked twice to the same assessment.");
        var links = expected.Select(id => new SignificantChangeWorkLinkView(
            current.CurrentVersionNumber, id, actorMemberId, actorDisplay.Trim(), linkedAt)).ToArray();
        var state = current with
        {
            Revision = Revision + 1,
            ReviewWorkLinks = Freeze(current.ReviewWorkLinks.Concat(links).ToArray()),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = linkedAt,
        };
        return Emit(state, actorMemberId, linkedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ImpactReviewWorkLinked(tenant, subject, revision, actor, snapshot, at),
            "work_link");
    }

    public CommandFailure? RecordSystemDescriptionDecision(long expectedRevision,
        string decision, Uuid decisionId, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset decidedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The significant change was not found.");
        decision = Clean(decision).ToLowerInvariant();
        rationale = Clean(rationale);
        if (current.SystemDescriptionDecisions.FirstOrDefault(item => item.DecisionId == decisionId) is { } prior)
            return prior.ContentVersion == current.CurrentVersionNumber && prior.Decision == decision &&
                   prior.Rationale == rationale && prior.DecidedByMemberId == actorMemberId &&
                   prior.DecidedByDisplay == actorDisplay.Trim() && prior.DecidedAt == decidedAt
                ? null
                : CommandFailure.StateConflict("A description decision identity cannot be reused.");
        if (expectedRevision != Revision)
            return StaleRevision();
        var assessment = current.CurrentAssessment;
        if (assessment is null || !assessment.RequiresSystemDescriptionReview || current.Status != "needs_review")
            return CommandFailure.StateConflict("This change has no unresolved system-description review.");
        if (!ValidActor(actorMemberId, actorDisplay, decidedAt) || decidedAt < assessment.AssessedAt ||
            decisionId == Uuid.Empty || !Bounded(rationale, 1, MaximumTextLength) ||
            decision is not ("approved_successor" or "no_change"))
            return Invalid("A description disposition requires an exact approved successor or recorded no-change decision.");
        if (actorMemberId == assessment.AssessedByMemberId)
            return CommandFailure.ActorProhibited("The change assessor cannot independently decide the description review.");
        var expectedWork = assessment.RelatedReviewWorkIds.Concat(assessment.RelatedCorrectiveActionIds);
        if (expectedWork.Any(id => !current.ReviewWorkLinks.Any(link =>
                link.ContentVersion == current.CurrentVersionNumber && link.WorkId == id)))
            return CommandFailure.StateConflict("Description review cannot be decided until all required follow-up work is linked.");

        var disposition = new SignificantChangeSystemDescriptionDecisionView(
            current.CurrentVersionNumber, decision, decisionId, rationale,
            actorMemberId, actorDisplay.Trim(), decidedAt);
        var state = current with
        {
            Status = "assessed",
            Revision = Revision + 1,
            SystemDescriptionDecisions = Append(current.SystemDescriptionDecisions, disposition),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = decidedAt,
        };
        return Emit(state, actorMemberId, decidedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new SignificantChangeSystemDescriptionDecisionRecorded(tenant, subject, revision, actor, snapshot, at),
            "description_decision");
    }

    void Apply(SignificantChangeRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "record");
    void Apply(SignificantChangeRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "revise");
    void Apply(SignificantChangeImpactAssessed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "assessment");
    void Apply(ImpactReviewWorkLinked ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "work_link");
    void Apply(SignificantChangeSystemDescriptionDecisionRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "description_decision");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        SignificantChangeView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.SignificantChangeId != Id ||
            state.Revision != revision || state.UpdatedAt != occurredAt || occurredAt == default ||
            revision != Revision + 1 || creating != (_current is null) || actorMemberId == Uuid.Empty ||
            state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "record" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The significant-change event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(SignificantChangeView state, Uuid actorMemberId, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, SignificantChangeView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null ||
                !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The significant-change transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return Invalid("The complete significant-change history exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(SignificantChangeView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId || state.SignificantChangeId != Id ||
            state.Revision <= 0 || state.Versions is null || state.Versions.Count is < 1 or > MaximumVersions ||
            state.Current is null || state.CurrentVersionNumber != state.Versions.Count ||
            state.Current.Version != state.CurrentVersionNumber || state.Assessments is null ||
            state.ReviewWorkLinks is null || state.SystemDescriptionDecisions is null ||
            state.Versions.Where((version, index) => version.Version != index + 1 ||
                NormalizeContent(version.Content) is not { } content || Digest(content) != version.ContentSha256 ||
                version.RecordedByMemberId == Uuid.Empty || !Bounded(version.RecordedByDisplay, 1, 200) ||
                version.RecordedAt == default || !Bounded(version.ChangeReason, 0, MaximumTextLength)).Any() ||
            !SameVersion(state.Current, state.Versions[^1]) ||
            state.Assessments.Count > state.Versions.Count ||
            state.Assessments.Select(item => item.ContentVersion).Distinct().Count() != state.Assessments.Count ||
            state.Assessments.Any(item => !ValidAssessment(item)) ||
            state.ReviewWorkLinks.Any(item => item.ContentVersion < 1 || item.ContentVersion > state.CurrentVersionNumber ||
                item.WorkId == Uuid.Empty || !ValidActor(item.LinkedByMemberId, item.LinkedByDisplay, item.LinkedAt)) ||
            state.ReviewWorkLinks.Select(item => (item.ContentVersion, item.WorkId)).Distinct().Count() != state.ReviewWorkLinks.Count ||
            state.SystemDescriptionDecisions.Any(item => item.ContentVersion < 1 ||
                item.ContentVersion > state.CurrentVersionNumber || item.Decision is not ("approved_successor" or "no_change") ||
                item.DecisionId == Uuid.Empty || !Bounded(item.Rationale, 1, MaximumTextLength) ||
                !ValidActor(item.DecidedByMemberId, item.DecidedByDisplay, item.DecidedAt)) ||
            state.SystemDescriptionDecisions.Select(item => item.ContentVersion).Distinct().Count() != state.SystemDescriptionDecisions.Count ||
            !ValidStatus(state) || !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt))
            return Invalid("The significant-change event contains an invalid state snapshot.");
        return null;
    }

    static bool ValidTransition(SignificantChangeView prior, SignificantChangeView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.SignificantChangeId != prior.SignificantChangeId ||
            !Prefix(prior.Assessments, next.Assessments) ||
            !Prefix(prior.ReviewWorkLinks, next.ReviewWorkLinks) ||
            !Prefix(prior.SystemDescriptionDecisions, next.SystemDescriptionDecisions))
            return false;
        return transition switch
        {
            "revise" => prior.Status != "closed" && next.Status == "recorded" &&
                next.Versions.Count == prior.Versions.Count + 1 && SameVersions(prior.Versions,
                    next.Versions.Take(prior.Versions.Count).ToArray()) &&
                next.Current!.RecordedByMemberId == actor && next.Current.RecordedAt == next.UpdatedAt &&
                next.Assessments.Count == prior.Assessments.Count &&
                next.ReviewWorkLinks.Count == prior.ReviewWorkLinks.Count &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count,
            "assessment" => next.Status is "assessed" or "needs_review" &&
                SameVersions(prior.Versions, next.Versions) &&
                next.Assessments.Count == prior.Assessments.Count + 1 &&
                next.Assessments[^1].ContentVersion == next.CurrentVersionNumber &&
                next.Assessments[^1].AssessedByMemberId == actor &&
                next.Assessments[^1].AssessedAt == next.UpdatedAt &&
                next.ReviewWorkLinks.Count == prior.ReviewWorkLinks.Count &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count,
            "work_link" => next.Status == prior.Status && SameVersions(prior.Versions, next.Versions) &&
                SameAssessments(prior.Assessments, next.Assessments) &&
                next.ReviewWorkLinks.Count > prior.ReviewWorkLinks.Count &&
                Prefix(prior.ReviewWorkLinks, next.ReviewWorkLinks) &&
                next.ReviewWorkLinks.Skip(prior.ReviewWorkLinks.Count).All(item =>
                    item.ContentVersion == next.CurrentVersionNumber && item.LinkedByMemberId == actor &&
                    item.LinkedAt == next.UpdatedAt) &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count,
            "description_decision" => prior.Status == "needs_review" && next.Status == "assessed" &&
                SameVersions(prior.Versions, next.Versions) && SameAssessments(prior.Assessments, next.Assessments) &&
                next.ReviewWorkLinks.Count == prior.ReviewWorkLinks.Count &&
                next.SystemDescriptionDecisions.Count == prior.SystemDescriptionDecisions.Count + 1 &&
                next.SystemDescriptionDecisions[^1].ContentVersion == next.CurrentVersionNumber &&
                next.SystemDescriptionDecisions[^1].DecidedByMemberId == actor &&
                next.SystemDescriptionDecisions[^1].DecidedAt == next.UpdatedAt,
            _ => false,
        };
    }

    static bool ValidAssessment(SignificantChangeImpactView item)
    {
        if (item.ContentVersion <= 0 || item.Significance is not ("significant" or "not_significant") ||
            !Bounded(item.Rationale, 1, MaximumTextLength) || item.ImpactDomains is null ||
            item.ImpactDomains.Count == 0 || item.ImpactDomains.Any(domain => !ImpactDomains.Contains(domain)) ||
            item.AffectedRecords is null || item.AffectedRecords.Count > MaximumReferences ||
            item.AffectedRecords.Any(record => !ValidAffectedRecord(record)) ||
            item.RelatedReviewWorkIds is null || !ValidIds(item.RelatedReviewWorkIds) ||
            item.RelatedCorrectiveActionIds is null || !ValidIds(item.RelatedCorrectiveActionIds) ||
            item.RelatedExceptionIds is null || !ValidIds(item.RelatedExceptionIds) ||
            item.EvidenceArtifactIds is null || !ValidIds(item.EvidenceArtifactIds) ||
            item.SystemDescriptionStatus != (item.RequiresSystemDescriptionReview ? "needs_review" : "not_required") ||
            item.RequiresSystemDescriptionReview && !item.ImpactDomains.Contains("system_description") ||
            !ValidActor(item.AssessedByMemberId, item.AssessedByDisplay, item.AssessedAt))
            return false;
        var hasBasis = item.ChangesSystemBoundary || item.ChangesServiceCommitment ||
                       item.ChangesKeyControl || item.ChangesInScopeInfrastructure;
        return item.Significance == (hasBasis ? "significant" : "not_significant") &&
               (item.Significance != "significant" || item.AffectedRecords.Count > 0);
    }

    static bool ValidStatus(SignificantChangeView state)
    {
        var assessment = state.Assessments.Count == 0 ? null : state.Assessments[^1];
        var current = assessment?.ContentVersion == state.CurrentVersionNumber ? assessment : null;
        var status = current is null ? "recorded" : current.RequiresSystemDescriptionReview &&
            state.SystemDescriptionDecisions.All(item => item.ContentVersion != state.CurrentVersionNumber)
            ? "needs_review" : "assessed";
        return state.Status == status;
    }

    static bool ValidInitialState(SignificantChangeView state) => state.Status == "recorded" &&
        state.Revision == 1 && state.CurrentVersionNumber == 1 && state.Assessments.Count == 0 &&
        state.ReviewWorkLinks.Count == 0 && state.SystemDescriptionDecisions.Count == 0;

    static SignificantChangeContent? NormalizeContent(SignificantChangeContent? content)
    {
        if (content is null || !Bounded(content.Title, 1, 300) ||
            !Bounded(content.Description, 1, MaximumTextLength) || content.EffectiveOn == default ||
            content.EffectiveThrough is { } through && through < content.EffectiveOn ||
            content.OwnerMemberId == Uuid.Empty || content.AffectedSystemIds is null ||
            content.AffectedSystemIds.Count is < 1 or > MaximumReferences || !ValidIds(content.AffectedSystemIds) ||
            content.EvidenceArtifactIds is null || !ValidIds(content.EvidenceArtifactIds))
            return null;
        var sourceSystem = Clean(content.SourceSystem);
        var sourceIdentifier = Clean(content.SourceIdentifier);
        var sourceUrl = Clean(content.SourceUrl);
        if ((sourceSystem.Length == 0) != (sourceIdentifier.Length == 0) ||
            sourceSystem.Length > 200 || sourceIdentifier.Length > 300 ||
            sourceUrl.Length > 2000 || sourceUrl.Length > 0 && !ValidUrl(sourceUrl))
            return null;
        var severity = Clean(content.Severity).ToLowerInvariant();
        var handlingClass = Clean(content.HandlingClass);
        if (handlingClass.Length == 0)
            handlingClass = "confidential";
        if (!Severities.Contains(severity) || !HandlingClasses.Contains(handlingClass))
            return null;
        return content with
        {
            SourceSystem = sourceSystem.Length == 0 ? null : sourceSystem,
            SourceIdentifier = sourceIdentifier.Length == 0 ? null : sourceIdentifier,
            SourceUrl = sourceUrl.Length == 0 ? null : sourceUrl,
            Title = content.Title.Trim(),
            Description = content.Description.Trim(),
            Severity = severity,
            HandlingClass = handlingClass,
            AffectedSystemIds = Freeze(content.AffectedSystemIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            EvidenceArtifactIds = Freeze(content.EvidenceArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
        };
    }

    static SignificantChangeImpactContent? NormalizeImpact(SignificantChangeImpactContent? content)
    {
        if (content is null || content.Significance is not ("significant" or "not_significant") ||
            !Bounded(content.Rationale, 1, MaximumTextLength) || content.ImpactDomains is null ||
            content.ImpactDomains.Count is < 1 or > 16 || content.AffectedRecords is null ||
            content.AffectedRecords.Count > MaximumReferences || content.AffectedRecords.Any(item => !ValidAffectedRecord(item)) ||
            content.AffectedRecords.Select(item => (item.RecordType, item.RecordId, item.VersionId)).Distinct().Count() != content.AffectedRecords.Count ||
            content.RelatedReviewWorkIds is null || !ValidIds(content.RelatedReviewWorkIds) ||
            content.RelatedCorrectiveActionIds is null || !ValidIds(content.RelatedCorrectiveActionIds) ||
            content.RelatedReviewWorkIds.Intersect(content.RelatedCorrectiveActionIds).Any() ||
            content.RelatedExceptionIds is null || !ValidIds(content.RelatedExceptionIds) ||
            content.EvidenceArtifactIds is null || !ValidIds(content.EvidenceArtifactIds) ||
            content.ImpactDomains.Any(item => !ImpactDomains.Contains(Clean(item))) ||
            content.ImpactDomains.Select(Clean).Distinct(StringComparer.Ordinal).Count() != content.ImpactDomains.Count)
            return null;
        return content with
        {
            Significance = content.Significance.Trim(),
            Rationale = content.Rationale.Trim(),
            ImpactDomains = Freeze(content.ImpactDomains.Select(Clean).Order(StringComparer.Ordinal).ToArray()),
            AffectedRecords = Freeze(content.AffectedRecords.OrderBy(item => item.RecordType, StringComparer.Ordinal)
                .ThenBy(item => IdKey(item.RecordId), StringComparer.Ordinal).ToArray()),
            RelatedReviewWorkIds = Freeze(content.RelatedReviewWorkIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            RelatedCorrectiveActionIds = Freeze(content.RelatedCorrectiveActionIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            RelatedExceptionIds = Freeze(content.RelatedExceptionIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            EvidenceArtifactIds = Freeze(content.EvidenceArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
        };
    }

    static bool ValidAffectedRecord(SignificantChangeAffectedRecordReference? item) => item is not null &&
        RecordTypes.Contains(item.RecordType) && item.RecordId != Uuid.Empty && item.VersionId != Uuid.Empty && item.Version > 0;
    static bool HasSignificanceBasis(SignificantChangeImpactContent content) => content.ChangesSystemBoundary ||
        content.ChangesServiceCommitment || content.ChangesKeyControl || content.ChangesInScopeInfrastructure;
    static SignificantChangeVersionView NewVersion(SignificantChangeContent content, int version,
        Uuid actor, string display, DateTimeOffset at, string reason) =>
        new(version, content, Digest(content), actor, display, at, reason);
    static bool SameInitial(SignificantChangeView state, SignificantChangeContent? content,
        Uuid actor, string display, DateTimeOffset at) => NormalizeContent(content) is { } normalized &&
        state.Current is { } current && current.ContentSha256 == Digest(normalized) &&
        current.RecordedByMemberId == actor && current.RecordedByDisplay == display.Trim() && current.RecordedAt == at;
    static bool SameRevision(SignificantChangeView state, SignificantChangeContent? content,
        string reason, Uuid actor, string display, DateTimeOffset at) => NormalizeContent(content) is { } normalized &&
        state.Current is { } current && current.ContentSha256 == Digest(normalized) && current.ChangeReason == reason &&
        current.RecordedByMemberId == actor && current.RecordedByDisplay == display.Trim() && current.RecordedAt == at;
    static bool SameAssessment(SignificantChangeView state, SignificantChangeImpactContent? content,
        Uuid actor, string display, DateTimeOffset at) => state.CurrentAssessment is { } existing &&
        NormalizeImpact(content) is { } normalized && existing.ContentVersion == state.CurrentVersionNumber &&
        existing.AssessedByMemberId == actor && existing.AssessedByDisplay == display.Trim() &&
        existing.AssessedAt == at && AssessmentDigest(existing) == Digest(normalized);
    static string AssessmentDigest(SignificantChangeImpactView item) => Digest(new
    {
        item.Significance,
        item.Rationale,
        item.ChangesSystemBoundary,
        item.ChangesServiceCommitment,
        item.ChangesKeyControl,
        item.ChangesInScopeInfrastructure,
        item.ImpactDomains,
        item.AffectedRecords,
        item.RelatedReviewWorkIds,
        item.RelatedCorrectiveActionIds,
        item.RelatedExceptionIds,
        item.EvidenceArtifactIds,
        item.RequiresSystemDescriptionReview,
    });
    static bool SameVersion(SignificantChangeVersionView left, SignificantChangeVersionView right) =>
        left.Version == right.Version && left.ContentSha256 == right.ContentSha256 &&
        left.RecordedByMemberId == right.RecordedByMemberId && left.RecordedByDisplay == right.RecordedByDisplay &&
        left.RecordedAt == right.RecordedAt && left.ChangeReason == right.ChangeReason;
    static bool SameVersions(IReadOnlyList<SignificantChangeVersionView> left,
        IReadOnlyList<SignificantChangeVersionView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => SameVersion(pair.First, pair.Second));
    static bool SameAssessments(IReadOnlyList<SignificantChangeImpactView> left,
        IReadOnlyList<SignificantChangeImpactView> right) => left.Count == right.Count &&
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
        VersionedRecordRules.StaleRevision("significant change", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) => Freeze(values.Append(value).ToArray());
    static SignificantChangeView Freeze(SignificantChangeView state) => state with
    {
        Current = state.Versions[^1],
        Versions = Freeze(state.Versions.ToArray()),
        Assessments = Freeze(state.Assessments.ToArray()),
        ReviewWorkLinks = Freeze(state.ReviewWorkLinks.ToArray()),
        SystemDescriptionDecisions = Freeze(state.SystemDescriptionDecisions.ToArray()),
    };
}
