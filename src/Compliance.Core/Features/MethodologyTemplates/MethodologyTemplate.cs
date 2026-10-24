using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Platform-owned authored template versions, independent review, approval, and retirement.</summary>
public sealed class MethodologyTemplate : Aggregate
{
    public NativeAcceptance ReplayAcceptance { get; }
    OperationProvenance? _pendingOperation;

    const int MaximumVersions = 64;
    const int MaximumRecords = 100;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    const int MaximumTextLength = 2000;
    static readonly HashSet<string> TemplateKinds = new(StringComparer.Ordinal)
        { "soc2", "iso27001", "custom" };
    static readonly HashSet<string> RecordKinds = new(StringComparer.Ordinal)
        { "control", "policy", "evidence_request", "risk" };

    MethodologyTemplateView? _current;
    MethodologyTemplateView? _initial;

    public long Revision => _current?.Revision ?? 0;
    public MethodologyTemplateView? Current => _current;

    public MethodologyTemplate(Uuid templateId, Uuid selectedCommandId = default)
        : base(templateId, new EventStreamAddress("bdgrz", "methodology-templates", templateId.ToString()))
    {
        ReplayAcceptance = new NativeAcceptance(selectedCommandId);
        On<MethodologyTemplateCreated>(Apply);
        On<MethodologyTemplateDraftRevised>(Apply);
        On<MethodologyTemplateDraftReviewed>(Apply);
        On<MethodologyTemplateVersionPublished>(Apply);
        On<MethodologyTemplateRetired>(Apply);
    }

    public CommandFailure? Create(MethodologyTemplateContent content, string changeNotes,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset createdAt)
    {
        if (_current is not null)
            return _initial is { } initial && SameCreate(initial, content, changeNotes, actorMemberId, actorDisplay, createdAt)
                ? null : CommandFailure.StateConflict("The methodology template identity already has different initial content.");
        if (Id == Uuid.Empty || !ValidActor(actorMemberId, actorDisplay, createdAt) ||
            NormalizeContent(content) is not { } normalized ||
            normalized.Records.Any(item => item.ContributionApprovalId is not null) ||
            !Bounded(changeNotes, 1, MaximumTextLength))
            return Invalid("A template requires original bounded content, change notes, and an attributed platform author.");
        var version = NewVersion(normalized, 1, changeNotes.Trim(), actorMemberId,
            actorDisplay.Trim(), createdAt) with
        { TemplateId = Id };
        var state = new MethodologyTemplateView(Id, "active", 1, [version], null, null,
            actorMemberId, actorDisplay.Trim(), createdAt, StateDigest(Id, "active", [version], null, null));
        return Emit(state, actorMemberId, createdAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateCreated(tenant, subject, revision, actor, snapshot, at), "create", true);
    }

    public CommandFailure? ReviseDraft(long expectedRevision, MethodologyTemplateContent content,
        string changeNotes, Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt,
        OperationProvenance? operation = null) =>
        ReviseDraftCore(expectedRevision, content, changeNotes, actorMemberId, actorDisplay, revisedAt, null, operation);

    public CommandFailure? ReviseDraftWithAcceptedContribution(long expectedRevision,
        MethodologyTemplateContent content, MethodologyTemplateContributionShareView contribution,
        string changeNotes, Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt,
        OperationProvenance? operation = null)
    {
        if (!ValidContributionShare(contribution))
            return Invalid("Template content can incorporate only an accepted, independently reviewed de-identified contribution.");
        var reference = new MethodologyTemplateContributionAcceptanceReference(contribution.AcceptanceId,
            contribution.ContentSha256, contribution.Review.ReviewedByMemberId,
            contribution.Review.ReviewedByDisplay, contribution.Review.ReviewedAt,
            contribution.AcceptedByMemberId, contribution.AcceptedByDisplay,
            contribution.AcceptanceRationale, contribution.AcceptedAt);
        return ReviseDraftCore(expectedRevision, content, changeNotes, actorMemberId,
            actorDisplay, revisedAt, reference, operation);
    }

    CommandFailure? ReviseDraftCore(long expectedRevision, MethodologyTemplateContent content,
        string changeNotes, Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt,
        MethodologyTemplateContributionAcceptanceReference? newContribution,
        OperationProvenance? operation = null)
    {
        if (AuthoredCommandReplay.TryReplay(ReplayAcceptance, operation, out var replayFailure))
            return replayFailure;
        _pendingOperation = operation;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The methodology template was not found.");
        if (SameDraftRevision(current, content, changeNotes, actorMemberId, actorDisplay, revisedAt))
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "active" || current.Versions.Count >= MaximumVersions ||
            !ValidActor(actorMemberId, actorDisplay, revisedAt) || revisedAt < current.UpdatedAt ||
            NormalizeContent(content) is not { } normalized || !Bounded(changeNotes, 1, MaximumTextLength))
            return Invalid("A template revision requires active status, bounded original content, change notes, and attribution.");
        var previous = current.Versions[^1];
        if (previous.Status == "reviewed")
            return CommandFailure.StateConflict("An independently approved draft must be published or returned before revision.");
        if (Digest(normalized) == previous.ContentSha256)
            return CommandFailure.StateConflict("A template revision must change content.");
        var acceptedContributions = current.Versions.SelectMany(item => item.AcceptedContributions ?? [])
            .GroupBy(item => item.AcceptanceId).Select(group => group.First()).ToArray();
        if (newContribution is not null)
        {
            var contributedRecords = normalized.Records.Where(item =>
                item.ContributionApprovalId == newContribution.AcceptanceId).ToArray();
            if (acceptedContributions.Any(item => item.AcceptanceId == newContribution.AcceptanceId) ||
                contributedRecords.Length != 1 || Digest(contributedRecords[0]) != newContribution.ContentSha256)
                return Invalid("The accepted contribution content and approval must match exactly once in the template revision.");
            acceptedContributions = [.. acceptedContributions, newContribution];
        }
        if (normalized.Records.Any(item => item.ContributionApprovalId is { } approvalId &&
            acceptedContributions.All(reference => reference.AcceptanceId != approvalId)))
            return Invalid("A de-identified contribution requires its accepted review provenance.");
        var versions = current.Versions.ToArray();
        if (previous.Status is "draft" or "changes_requested")
            versions[^1] = previous with { Status = "superseded" };
        var version = NewVersion(normalized, current.Versions.Count + 1, changeNotes.Trim(),
            actorMemberId, actorDisplay.Trim(), revisedAt, acceptedContributions);
        var updatedVersions = Append(versions, version);
        var state = State(current, "active", updatedVersions, null, null,
            actorMemberId, actorDisplay.Trim(), revisedAt);
        return Emit(state, actorMemberId, revisedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateDraftRevised(tenant, subject, revision, actor, snapshot, at), "revise");
    }

    public CommandFailure? ReviewDraft(long expectedRevision, string outcome, string comments,
        Uuid reviewerMemberId, string reviewerDisplay, DateTimeOffset reviewedAt,
        OperationProvenance? operation = null)
    {
        if (AuthoredCommandReplay.TryReplay(ReplayAcceptance, operation, out var replayFailure))
            return replayFailure;
        _pendingOperation = operation;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The methodology template was not found.");
        outcome = Clean(outcome).ToLowerInvariant();
        comments = Clean(comments);
        if (current.Versions[^1].Review is { } priorReview)
            return priorReview.Outcome == outcome && priorReview.Comments == comments &&
                priorReview.ReviewedByMemberId == reviewerMemberId && priorReview.ReviewedByDisplay == Clean(reviewerDisplay) &&
                priorReview.ReviewedAt == reviewedAt
                ? null : CommandFailure.StateConflict("A methodology-template review decision is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        var draft = current.Versions[^1];
        if (current.Status != "active" || draft.Status != "draft" ||
            outcome is not ("approved" or "changes_requested") ||
            !Bounded(comments, 1, MaximumTextLength) || !ValidActor(reviewerMemberId, reviewerDisplay, reviewedAt) ||
            reviewedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("Only the current draft can receive a bounded, attributable review decision.");
        if (reviewerMemberId == draft.AuthoredByMemberId)
            return CommandFailure.ActorProhibited("A methodology-template author cannot review their own draft.");
        var review = new MethodologyTemplateReviewView(Uuid.CreateVersion4(), outcome,
            comments, reviewerMemberId, reviewerDisplay.Trim(), reviewedAt);
        var versions = current.Versions.ToArray();
        versions[^1] = draft with { Status = outcome == "approved" ? "reviewed" : "changes_requested", Review = review };
        var state = State(current, "active", versions, null, null,
            reviewerMemberId, reviewerDisplay.Trim(), reviewedAt);
        return Emit(state, reviewerMemberId, reviewedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateDraftReviewed(tenant, subject, revision, actor, snapshot, at), "review");
    }

    public CommandFailure? ApproveVersion(long expectedRevision, string rationale,
        Uuid approverMemberId, string approverDisplay, DateTimeOffset approvedAt,
        OperationProvenance? operation = null)
    {
        if (AuthoredCommandReplay.TryReplay(ReplayAcceptance, operation, out var replayFailure))
            return replayFailure;
        _pendingOperation = operation;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The methodology template was not found.");
        rationale = Clean(rationale);
        if (current.Versions[^1].Status == "published")
        {
            var published = current.Versions[^1];
            return published.ApprovalRationale == rationale && published.ApprovedByMemberId == approverMemberId &&
                published.ApprovedByDisplay == Clean(approverDisplay) && published.ApprovedAt == approvedAt
                ? null : CommandFailure.StateConflict("A published template version cannot be re-approved with different details.");
        }
        if (expectedRevision != Revision)
            return StaleRevision();
        var draft = current.Versions[^1];
        if (current.Status != "active" || draft.Status != "reviewed" || draft.Review is not { Outcome: "approved" } review ||
            !Bounded(rationale, 1, MaximumTextLength) || !ValidActor(approverMemberId, approverDisplay, approvedAt) ||
            approvedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("Publishing requires an approved independent review and an attributable approval rationale.");
        if (approverMemberId == draft.AuthoredByMemberId || approverMemberId == review.ReviewedByMemberId)
            return CommandFailure.ActorProhibited("The author and reviewer cannot approve a template version.");
        var versions = current.Versions.ToArray();
        versions[^1] = draft with
        {
            Status = "published",
            ApprovedByMemberId = approverMemberId,
            ApprovedByDisplay = approverDisplay.Trim(),
            ApprovedAt = approvedAt,
            ApprovalRationale = rationale,
        };
        var state = State(current, "active", versions, null, null,
            approverMemberId, approverDisplay.Trim(), approvedAt);
        return Emit(state, approverMemberId, approvedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateVersionPublished(tenant, subject, revision, actor, snapshot, at), "publish");
    }

    public CommandFailure? Retire(long expectedRevision, DateOnly effectiveOn, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset retiredAt,
        OperationProvenance? operation = null)
    {
        if (AuthoredCommandReplay.TryReplay(ReplayAcceptance, operation, out var replayFailure))
            return replayFailure;
        _pendingOperation = operation;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The methodology template was not found.");
        rationale = Clean(rationale);
        if (current.Status == "retired")
            return current.RetiredEffectiveOn == effectiveOn && current.RetirementRationale == rationale
                && current.ActorMemberId == actorMemberId && current.ActorDisplay == Clean(actorDisplay) &&
                current.UpdatedAt == retiredAt
                ? null : CommandFailure.StateConflict("A retired methodology template cannot be changed.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "active" || current.Versions.All(item => item.Status != "published") ||
            effectiveOn == default || !Bounded(rationale, 1, MaximumTextLength) ||
            !ValidActor(actorMemberId, actorDisplay, retiredAt) || retiredAt < current.UpdatedAt)
            return CommandFailure.StateConflict("Retirement requires a published template and an attributable effective date and rationale.");
        var state = State(current, "retired", current.Versions, rationale, effectiveOn,
            actorMemberId, actorDisplay.Trim(), retiredAt);
        return Emit(state, actorMemberId, retiredAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateRetired(tenant, subject, revision, actor, snapshot, at), "retire");
    }

    void Apply(MethodologyTemplateCreated ev)
    {
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
            ev.ActorMemberId, ev.State, ev.OccurredAt, true, "create");
        _initial = Freeze(ev.State);
    }
    void Apply(MethodologyTemplateDraftRevised ev)
    {
        AuthoredCommandReplay.Observe(this, ReplayAcceptance, ev, ev.Operation);
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "revise");
    }
    void Apply(MethodologyTemplateDraftReviewed ev)
    {
        AuthoredCommandReplay.Observe(this, ReplayAcceptance, ev, ev.Operation);
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "review");
    }
    void Apply(MethodologyTemplateVersionPublished ev)
    {
        AuthoredCommandReplay.Observe(this, ReplayAcceptance, ev, ev.Operation);
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "publish");
    }
    void Apply(MethodologyTemplateRetired ev)
    {
        AuthoredCommandReplay.Observe(this, ReplayAcceptance, ev, ev.Operation);
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "retire");
    }

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        MethodologyTemplateView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != Uuid.Empty || subjectId != Id || state is null || state.TemplateId != Id ||
            state.Revision != revision || state.UpdatedAt != occurredAt || revision != Revision + 1 ||
            creating != (_current is null) || actorMemberId == Uuid.Empty || state.ActorMemberId != actorMemberId ||
            ValidateState(state) is not null || _current is null && (transition != "create" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The methodology-template event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(MethodologyTemplateView state, Uuid actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, MethodologyTemplateView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null || !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The methodology-template transition is invalid.");
        var ev = createEvent(Uuid.Empty, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        ev = ev switch
        {
            MethodologyTemplateDraftRevised fact => fact with { Operation = _pendingOperation },
            MethodologyTemplateDraftReviewed fact => fact with { Operation = _pendingOperation },
            MethodologyTemplateVersionPublished fact => fact with { Operation = _pendingOperation },
            MethodologyTemplateRetired fact => fact with { Operation = _pendingOperation },
            _ => ev,
        };
        if (DomainEventPayload.Size(ev) > MaximumEventBytes)
            return Invalid("The methodology-template snapshot exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    static bool ValidTransition(MethodologyTemplateView prior, MethodologyTemplateView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TemplateId != prior.TemplateId ||
            next.Versions.Count < prior.Versions.Count || next.Versions.Count > prior.Versions.Count + 1 ||
            next.UpdatedAt < prior.UpdatedAt ||
            !ValidVersionPrefix(prior.Versions, next.Versions, transition) ||
            next.ActorMemberId != actor || next.ActorDisplay.Length == 0)
            return false;
        return transition switch
        {
            "revise" => prior.Status == "active" && next.Status == "active" &&
                next.Versions.Count == prior.Versions.Count + 1 && next.Versions[^1].Status == "draft" &&
                next.Versions[^1].AuthoredByMemberId == actor && next.Versions[^1].AuthoredAt == next.UpdatedAt,
            "review" => prior.Status == "active" && next.Status == "active" &&
                next.Versions.Count == prior.Versions.Count && next.Versions[^1].Review is { } review &&
                review.ReviewedByMemberId == actor && review.ReviewedAt == next.UpdatedAt,
            "publish" => prior.Status == "active" && next.Status == "active" &&
                next.Versions.Count == prior.Versions.Count && next.Versions[^1] is
                { Status: "published", ApprovedByMemberId: { } approved, ApprovedAt: { } at } &&
                approved == actor && at == next.UpdatedAt &&
                next.Versions[^1].ApprovalRationale is { } rationale && Bounded(rationale, 1, MaximumTextLength),
            "retire" => prior.Status == "active" && next.Status == "retired" &&
                next.Versions.Count == prior.Versions.Count && next.RetirementRationale is not null &&
                next.RetiredEffectiveOn is not null,
            _ => false,
        };
    }

    static bool ValidVersionPrefix(IReadOnlyList<MethodologyTemplateVersionView> prior,
        IReadOnlyList<MethodologyTemplateVersionView> next, string transition)
    {
        for (var index = 0; index < prior.Count; index++)
        {
            var before = prior[index];
            var after = next[index];
            if (before.VersionId != after.VersionId || before.VersionNumber != after.VersionNumber ||
                before.ContentSha256 != after.ContentSha256 || Digest(before.Content) != Digest(after.Content) ||
                before.AuthoredByMemberId != after.AuthoredByMemberId || before.AuthoredByDisplay != after.AuthoredByDisplay ||
                before.AuthoredAt != after.AuthoredAt || before.ChangeNotes != after.ChangeNotes ||
                Digest(before.AcceptedContributions ?? []) != Digest(after.AcceptedContributions ?? []) ||
                transition != "publish" && before.ApprovalRationale != after.ApprovalRationale)
                return false;
            if (index < prior.Count - 1 && Digest(before) != Digest(after))
                return false;
            if (index == prior.Count - 1 && transition switch
            {
                "review" => after.Status is not ("reviewed" or "changes_requested") || before.Review is not null ||
                    after.ApprovedByMemberId is not null || after.ApprovalRationale is not null ||
                    Digest(before.AcceptedContributions ?? []) != Digest(after.AcceptedContributions ?? []),
                "publish" => before.Status != "reviewed" || Digest(before.Review) != Digest(after.Review) ||
                    after.Status != "published" || after.ApprovedByMemberId is null ||
                    after.ApprovalRationale is null || before.ApprovalRationale is not null ||
                    Digest(before.AcceptedContributions ?? []) != Digest(after.AcceptedContributions ?? []),
                "revise" when next.Count > prior.Count => before.Status is "draft" or "changes_requested"
                    ? after.Status != "superseded" : Digest(before) != Digest(after),
                _ => Digest(before) != Digest(after),
            })
                return false;
        }
        return true;
    }

    static CommandFailure? ValidateState(MethodologyTemplateView state)
    {
        if (state.TemplateId == Uuid.Empty || state.Revision <= 0 || state.Versions is null ||
            state.Versions.Count is < 1 or > MaximumVersions ||
            state.Versions.Where((version, index) => !ValidVersion(version, state.TemplateId) ||
                version.VersionNumber != index + 1).Any() ||
            state.Versions.Select(item => item.VersionId).Distinct().Count() != state.Versions.Count ||
            state.Status is not ("active" or "retired") ||
            (state.Status == "retired"
                ? !Bounded(state.RetirementRationale, 1, MaximumTextLength) || state.RetiredEffectiveOn is null
                : state.RetirementRationale is not null || state.RetiredEffectiveOn is not null) ||
            !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt) ||
            state.ContentSha256 != StateDigest(state.TemplateId, state.Status, state.Versions,
                state.RetirementRationale, state.RetiredEffectiveOn))
            return Invalid("The methodology-template snapshot is invalid.");
        return null;
    }

    static bool ValidVersion(MethodologyTemplateVersionView version, Uuid templateId)
    {
        if (version is null || version.TemplateId != templateId || version.VersionId == Uuid.Empty ||
            NormalizeContent(version.Content) is not { } content || Digest(content) != version.ContentSha256 ||
            !ValidActor(version.AuthoredByMemberId, version.AuthoredByDisplay, version.AuthoredAt) ||
            !Bounded(version.ChangeNotes, 1, MaximumTextLength) ||
            version.Status is not ("draft" or "reviewed" or "changes_requested" or "published" or "superseded"))
            return false;
        if (version.Review is { } review &&
            (review.ReviewId == Uuid.Empty || review.Outcome is not ("approved" or "changes_requested") ||
             !Bounded(review.Comments, 1, MaximumTextLength) ||
             !ValidActor(review.ReviewedByMemberId, review.ReviewedByDisplay, review.ReviewedAt) ||
             review.ReviewedByMemberId == version.AuthoredByMemberId || review.ReviewedAt < version.AuthoredAt))
            return false;
        if (version.Status == "reviewed" && version.Review?.Outcome != "approved" ||
            version.Status == "changes_requested" && version.Review?.Outcome != "changes_requested")
            return false;
        var acceptedContributions = version.AcceptedContributions ?? [];
        if (acceptedContributions.Select(item => item.AcceptanceId).Distinct().Count() != acceptedContributions.Count ||
            acceptedContributions.Any(item => !ValidAcceptanceReference(item)) ||
            version.Content.Records.Any(record => record.ContributionApprovalId is { } acceptanceId &&
                acceptedContributions.All(item => item.AcceptanceId != acceptanceId ||
                    item.ContentSha256 != Digest(record))))
            return false;
        return version.Status == "published"
            ? version.Review?.Outcome == "approved" && version.ApprovedByMemberId is { } approver &&
              approver != version.AuthoredByMemberId && approver != version.Review.ReviewedByMemberId &&
              version.ApprovedByDisplay is { } display && Bounded(display, 1, 200) &&
              version.ApprovedAt is { } at && at >= version.Review.ReviewedAt &&
              Bounded(version.ApprovalRationale, 1, MaximumTextLength)
            : version.ApprovedByMemberId is null && version.ApprovedByDisplay is null &&
              version.ApprovedAt is null && version.ApprovalRationale is null;
    }

    static MethodologyTemplateContent? NormalizeContent(MethodologyTemplateContent? content)
    {
        if (content is null || !Bounded(content.Name, 1, 200) || !Bounded(content.Description, 1, MaximumTextLength) ||
            content.Records is null || content.Records.Count is < 1 or > MaximumRecords)
            return null;
        var kind = Clean(content.TemplateKind).ToLowerInvariant();
        if (!TemplateKinds.Contains(kind) || content.Records.Any(item => !ValidRecord(item)) ||
            content.Records.Select(item => item.EntryKey.Trim()).Distinct(StringComparer.Ordinal).Count() != content.Records.Count ||
            content.Records.Where(item => item.ContributionApprovalId is not null)
                .Select(item => item.ContributionApprovalId).Distinct().Count() !=
                content.Records.Count(item => item.ContributionApprovalId is not null))
            return null;
        return content with
        {
            Name = content.Name.Trim(),
            Description = content.Description.Trim(),
            TemplateKind = kind,
            Records = Freeze(content.Records.Select(item => item with
            {
                EntryKey = item.EntryKey.Trim(),
                RecordKind = item.RecordKind.Trim().ToLowerInvariant(),
                Title = item.Title.Trim(),
                Summary = item.Summary.Trim(),
                CriteriaReferences = Freeze(item.CriteriaReferences.Select(reference => reference with
                { Identifier = reference.Identifier.Trim().ToUpperInvariant() })
                    .OrderBy(reference => reference.EditionId.ToString(), StringComparer.Ordinal)
                    .ThenBy(reference => reference.Identifier, StringComparer.Ordinal).ToArray()),
            }).OrderBy(item => item.EntryKey, StringComparer.Ordinal).ToArray()),
        };
    }

    static bool ValidRecord(MethodologyTemplateRecordContent item) => item is not null &&
        Token(item.EntryKey, 100) && RecordKinds.Contains(Clean(item.RecordKind).ToLowerInvariant()) &&
        Bounded(item.Title, 1, 200) && Bounded(item.Summary, 1, MaximumTextLength) &&
        item.ContributionApprovalId != Uuid.Empty &&
        item.CriteriaReferences is not null && item.CriteriaReferences.Count <= 50 &&
        item.CriteriaReferences.All(reference => reference is not null && reference.EditionId != Uuid.Empty &&
            Token(reference.Identifier, 100));

    static bool ValidInitialState(MethodologyTemplateView state) => state.Status == "active" &&
        state.Versions.Count > 0 && state.Versions[0].VersionNumber == 1 &&
        state.Versions[0].Status == "draft" && state.Versions[0].Review is null &&
        state.Versions[0].ApprovedByMemberId is null && state.Versions[0].AcceptedContributions is { Count: 0 } &&
        state.Versions[0].Content.Records.All(item => item.ContributionApprovalId is null);

    static bool SameCreate(MethodologyTemplateView state, MethodologyTemplateContent content,
        string notes, Uuid actor, string display, DateTimeOffset at) =>
        state.Revision == 1 && state.Versions.Count == 1 &&
        NormalizeContent(content) is { } normalized && Digest(normalized) == state.Versions[0].ContentSha256 &&
        state.Versions[0].ChangeNotes == Clean(notes) && state.Versions[0].AuthoredByMemberId == actor;

    static bool SameDraftRevision(MethodologyTemplateView state, MethodologyTemplateContent content,
        string notes, Uuid actor, string display, DateTimeOffset at) =>
        NormalizeContent(content) is { } normalized && state.Versions[^1] is { } version &&
        Digest(normalized) == version.ContentSha256 && version.ChangeNotes == Clean(notes) &&
        version.AuthoredByMemberId == actor && version.AuthoredByDisplay == Clean(display) && version.AuthoredAt == at;

    static MethodologyTemplateVersionView NewVersion(MethodologyTemplateContent content, long number,
        string notes, Uuid actor, string display, DateTimeOffset at,
        IReadOnlyList<MethodologyTemplateContributionAcceptanceReference>? acceptedContributions = null) => new(
        Uuid.Empty, Uuid.CreateVersion4(), number, "draft", content, Digest(content), actor,
        display, at, null, null, null, null, notes, Freeze((acceptedContributions ?? []).ToArray()));

    static MethodologyTemplateView State(MethodologyTemplateView current, string status,
        IReadOnlyList<MethodologyTemplateVersionView> versions, string? retirementRationale,
        DateOnly? retiredEffectiveOn, Uuid actor, string display, DateTimeOffset at)
    {
        var ownedVersions = Freeze(versions.Select(version => version with { TemplateId = current.TemplateId }).ToArray());
        return new MethodologyTemplateView(current.TemplateId, status, current.Revision + 1,
            ownedVersions, retirementRationale, retiredEffectiveOn, actor, display, at,
            StateDigest(current.TemplateId, status, ownedVersions, retirementRationale, retiredEffectiveOn));
    }

    static MethodologyTemplateView Freeze(MethodologyTemplateView state) => state with
    {
        Versions = Freeze(state.Versions.Select(version => version with
        {
            Content = FreezeContent(version.Content),
            AcceptedContributions = Freeze((version.AcceptedContributions ?? []).ToArray()),
        }).ToArray()),
    };

    static MethodologyTemplateContent FreezeContent(MethodologyTemplateContent content) => content with
    {
        Records = Freeze(content.Records.Select(item => item with
        {
            CriteriaReferences = Freeze(item.CriteriaReferences.ToArray()),
        }).ToArray()),
    };

    static bool ValidContributionShare(MethodologyTemplateContributionShareView contribution) =>
        contribution is not null && contribution.AcceptanceId != Uuid.Empty &&
        contribution.Content is { } content && content.ContributionApprovalId == contribution.AcceptanceId &&
        ValidRecord(content) && Digest(content) == contribution.ContentSha256 &&
        contribution.Review is { Outcome: "approved" } review && review.ReviewId != Uuid.Empty &&
        ValidActor(review.ReviewedByMemberId, review.ReviewedByDisplay, review.ReviewedAt) &&
        Bounded(review.Comments, 1, MaximumTextLength) &&
        contribution.AcceptedByMemberId != Uuid.Empty &&
        contribution.AcceptedByMemberId != review.ReviewedByMemberId &&
        ValidActor(contribution.AcceptedByMemberId, contribution.AcceptedByDisplay, contribution.AcceptedAt) &&
        Bounded(contribution.AcceptanceRationale, 1, MaximumTextLength) &&
        contribution.AcceptedAt >= review.ReviewedAt;

    static bool ValidAcceptanceReference(MethodologyTemplateContributionAcceptanceReference reference) =>
        reference is not null && reference.AcceptanceId != Uuid.Empty && Sha256(reference.ContentSha256) &&
        ValidActor(reference.ReviewedByMemberId, reference.ReviewedByDisplay, reference.ReviewedAt) &&
        reference.AcceptedByMemberId != Uuid.Empty && reference.AcceptedByMemberId != reference.ReviewedByMemberId &&
        ValidActor(reference.AcceptedByMemberId, reference.AcceptedByDisplay, reference.AcceptedAt) &&
        Bounded(reference.AcceptanceRationale, 1, MaximumTextLength) && reference.AcceptedAt >= reference.ReviewedAt;

    static string StateDigest(Uuid templateId, string status,
        IReadOnlyList<MethodologyTemplateVersionView> versions, string? retirementRationale,
        DateOnly? retiredEffectiveOn) => Digest(new { templateId, status, versions, retirementRationale, retiredEffectiveOn });
    static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum && !value.Any(char.IsControl);
    static bool Token(string? value, int max) => Bounded(value, 1, max) && value!.Trim().All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty && Bounded(display, 1, 200) && at != default;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("methodology template", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IEnumerable<T> values, T item) => Freeze(values.Append(item).ToArray());
}
