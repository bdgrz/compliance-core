using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Owns one tenant/program application, its exact template provenance, and downstream draft receipts.</summary>
public sealed class TemplateApplication : Aggregate
{
    const int MaximumDrafts = 100;
    const int MaximumSuccessorDecisions = 64;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    const int MaximumTextLength = 2000;

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    TemplateApplicationView? _current;

    public long Revision => _current?.Revision ?? 0;
    public TemplateApplicationView? Current => _current;

    public TemplateApplication(Uuid tenantId, Uuid programId, Uuid applicationId)
        : base(applicationId, new EventStreamAddress(tenantId.ToString(), "template-applications",
            applicationId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<MethodologyTemplateApplied>(Apply);
        On<TemplateApplicationDraftCreated>(Apply);
        On<TemplateSuccessorAdoptionProposed>(Apply);
        On<TemplateSuccessorDispositionRecorded>(Apply);
    }

    public CommandFailure? Apply(MethodologyTemplateVersionView sourceVersion, string applicationKey,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset appliedAt)
    {
        if (_current is { } existing)
            return SameApplication(existing, sourceVersion, applicationKey, actorMemberId, actorDisplay, appliedAt)
                ? null : CommandFailure.StateConflict("A template application identity cannot be reused for another version or tenant intent.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, appliedAt) ||
            !Token(applicationKey, 200) || NormalizePublishedVersion(sourceVersion) is not { } normalized ||
            normalized.Content.Records.Count > MaximumDrafts)
            return Invalid("A template application requires one exact published version, one tenant/program, and an attributed requester.");
        var drafts = normalized.Content.Records.Select(record => new TemplateApplicationDraftView(
            record.EntryKey, record.RecordKind, Uuid.CreateVersion5(Id, "template-entry:" + record.EntryKey),
            "draft_pending", null, null, null)).ToArray();
        var state = new TemplateApplicationView(_tenantId, _programId, Id, "applied", 1,
            normalized.TemplateId, normalized.VersionId, normalized.VersionNumber,
            normalized.ContentSha256, applicationKey.Trim(), string.Empty, normalized, Freeze(drafts),
            [], [], false, actorMemberId, actorDisplay.Trim(), appliedAt, actorMemberId,
            actorDisplay.Trim(), appliedAt);
        state = state with { ContentSha256 = DigestState(state) };
        return Emit(state, actorMemberId, appliedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateApplied(tenant, subject, revision, actor, snapshot, at), "apply", true);
    }

    public CommandFailure? RecordDraftCreated(long expectedRevision, string entryKey,
        Uuid createdRecordId, long createdRecordVersion, string createdRecordSha256,
        string lifecycleStatus, Uuid actorMemberId, string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The template application was not found.");
        entryKey = Clean(entryKey);
        lifecycleStatus = Clean(lifecycleStatus).ToLowerInvariant();
        var draft = current.Drafts.FirstOrDefault(item => item.EntryKey == entryKey);
        if (draft is { Status: "draft_created" } prior)
            return prior.CreatedRecordId == createdRecordId && prior.CreatedRecordVersion == createdRecordVersion &&
                prior.CreatedRecordSha256 == NormalizeHash(createdRecordSha256) && lifecycleStatus == "draft"
                ? null : CommandFailure.StateConflict("A template application entry can only retain one exact draft-creation receipt.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (draft is null || draft.Status != "draft_pending" || createdRecordId != draft.PlannedRecordId ||
            createdRecordVersion <= 0 || !Sha256(createdRecordSha256) || lifecycleStatus != "draft" ||
            !ValidActor(actorMemberId, actorDisplay, recordedAt) || recordedAt < current.UpdatedAt)
            return Invalid("An application receipt must identify the planned record, exact content, and draft lifecycle state.");
        var drafts = current.Drafts.Select(item => item.EntryKey == entryKey
            ? item with
            {
                Status = "draft_created",
                CreatedRecordId = createdRecordId,
                CreatedRecordVersion = createdRecordVersion,
                CreatedRecordSha256 = NormalizeHash(createdRecordSha256),
            }
            : item).ToArray();
        var state = State(current, drafts.All(item => item.Status == "draft_created") ? "drafts_created" : "applied",
            drafts, current.SuccessorProposals, current.SuccessorDecisions,
            actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(state, actorMemberId, recordedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TemplateApplicationDraftCreated(tenant, subject, revision, actor, snapshot, at), "draft_created");
    }

    public CommandFailure? ProposeSuccessor(long expectedRevision,
        MethodologyTemplateVersionView successor, string proposedDisposition, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset proposedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The template application was not found.");
        var normalized = NormalizePublishedVersion(successor);
        proposedDisposition = Clean(proposedDisposition).ToLowerInvariant();
        if (normalized is not null && current.SuccessorProposals.FirstOrDefault(item =>
                item.Version.VersionId == normalized.VersionId) is { } existingProposal)
            return Digest(existingProposal.Version) == Digest(normalized) &&
                existingProposal.ContentSha256 == normalized.ContentSha256 &&
                existingProposal.ProposedDisposition == proposedDisposition &&
                existingProposal.ProposedByMemberId == actorMemberId &&
                existingProposal.ProposedByDisplay == Clean(actorDisplay) &&
                existingProposal.ProposedAt == proposedAt
                ? null : CommandFailure.StateConflict("A successor proposal version cannot be reused with different details.");
        if (expectedRevision != Revision)
            return StaleRevision();
        var priorProposal = LastProposal(current.SuccessorProposals);
        var pending = priorProposal is not null && !current.SuccessorDecisions.Any(item =>
            item.TemplateVersionId == priorProposal.Version.VersionId);
        if (current.Status is not ("applied" or "drafts_created") || pending ||
            normalized is null || proposedDisposition is not ("adopt" or "adapt" or "decline") ||
            normalized.TemplateId != current.TemplateId ||
            normalized.VersionNumber <= (priorProposal?.Version.VersionNumber ?? current.TemplateVersionNumber) ||
            current.SuccessorProposals.Any(item => item.Version.VersionId == normalized.VersionId) ||
            !ValidActor(actorMemberId, actorDisplay, proposedAt) || proposedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("A successor proposal must be a later published version of the same platform template.");
        var proposal = new TemplateApplicationSuccessorProposalView(normalized,
            normalized.ContentSha256, proposedDisposition, actorDisplay.Trim(), actorMemberId, proposedAt);
        var proposals = Append(current.SuccessorProposals, proposal);
        var state = State(current, current.Status, current.Drafts, proposals, current.SuccessorDecisions,
            actorMemberId, actorDisplay.Trim(), proposedAt);
        return Emit(state, actorMemberId, proposedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TemplateSuccessorAdoptionProposed(tenant, subject, revision, actor, snapshot, at), "successor_proposed");
    }

    public CommandFailure? RecordSuccessorDisposition(long expectedRevision, string disposition,
        string rationale, Uuid actorMemberId, string actorDisplay, DateTimeOffset decidedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The template application was not found.");
        disposition = Clean(disposition).ToLowerInvariant();
        rationale = Clean(rationale);
        var proposal = LastProposal(current.SuccessorProposals);
        var priorDecision = proposal is null ? null : current.SuccessorDecisions.FirstOrDefault(item =>
            item.TemplateVersionId == proposal.Version.VersionId);
        if (priorDecision is not null)
            return priorDecision.Disposition == disposition && priorDecision.Rationale == rationale &&
                priorDecision.DecidedByMemberId == actorMemberId &&
                priorDecision.DecidedByDisplay == Clean(actorDisplay) && priorDecision.DecidedAt == decidedAt
                ? null : CommandFailure.StateConflict("A successor disposition is immutable once recorded.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (proposal is null || current.SuccessorDecisions.Any(item => item.TemplateVersionId == proposal.Version.VersionId) ||
            disposition is not ("adopt" or "adapt" or "decline") || !Bounded(rationale, 1, MaximumTextLength) ||
            !ValidActor(actorMemberId, actorDisplay, decidedAt) || decidedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("A successor disposition requires one pending proposal and an attributable rationale.");
        var decision = new TemplateApplicationSuccessorDecisionView(proposal.Version.VersionId,
            proposal.Version.VersionNumber, disposition, rationale, actorMemberId, actorDisplay.Trim(), decidedAt);
        var decisions = Append(current.SuccessorDecisions, decision);
        var state = State(current, current.Status, current.Drafts, current.SuccessorProposals, decisions,
            actorMemberId, actorDisplay.Trim(), decidedAt);
        return Emit(state, actorMemberId, decidedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TemplateSuccessorDispositionRecorded(tenant, subject, revision, actor, snapshot, at), "successor_disposition");
    }

    void Apply(MethodologyTemplateApplied ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, true, "apply");
    void Apply(TemplateApplicationDraftCreated ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "draft_created");
    void Apply(TemplateSuccessorAdoptionProposed ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "successor_proposed");
    void Apply(TemplateSuccessorDispositionRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "successor_disposition");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        TemplateApplicationView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null || state.TenantId != _tenantId ||
            state.ProgramId != _programId || state.TemplateApplicationId != Id || state.Revision != revision ||
            state.UpdatedAt != occurredAt || revision != Revision + 1 || creating != (_current is null) ||
            actorMemberId == Uuid.Empty || state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "apply" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The template-application event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(TemplateApplicationView state, Uuid actorMemberId, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, TemplateApplicationView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null || !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The template-application transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (DomainEventPayload.Size(ev) > MaximumEventBytes)
            return Invalid("The template-application snapshot exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    static bool ValidTransition(TemplateApplicationView prior, TemplateApplicationView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.TemplateApplicationId != prior.TemplateApplicationId ||
            next.TemplateId != prior.TemplateId || next.TemplateVersionId != prior.TemplateVersionId ||
            next.TemplateContentSha256 != prior.TemplateContentSha256 || next.ApplicationKey != prior.ApplicationKey ||
            next.UpdatedAt < prior.UpdatedAt ||
            next.ContentSha256 != DigestState(next with { ContentSha256 = string.Empty }) ||
            next.RequestedByMemberId != prior.RequestedByMemberId || next.RequestedAt != prior.RequestedAt ||
            next.ActorMemberId != actor || next.IsActivated ||
            !DraftsCompatible(prior.Drafts, next.Drafts, transition) ||
            !Prefix(prior.SuccessorDecisions, next.SuccessorDecisions))
            return false;
        return transition switch
        {
            "draft_created" => Prefix(prior.SuccessorProposals, next.SuccessorProposals) &&
                next.Drafts.Count == prior.Drafts.Count && next.Drafts.Count(item => item.Status == "draft_created") ==
                    prior.Drafts.Count(item => item.Status == "draft_created") + 1,
            "successor_proposed" => next.SuccessorProposals.Count == prior.SuccessorProposals.Count + 1 &&
                Prefix(prior.SuccessorProposals, next.SuccessorProposals) &&
                next.SuccessorProposals[^1].ProposedByMemberId == actor &&
                next.SuccessorProposals[^1].ProposedAt == next.UpdatedAt &&
                next.SuccessorDecisions.Count == prior.SuccessorDecisions.Count &&
                DraftsCompatible(prior.Drafts, next.Drafts, "unchanged"),
            "successor_disposition" => Prefix(prior.SuccessorProposals, next.SuccessorProposals) &&
                LastProposal(next.SuccessorProposals) is { } proposal &&
                !prior.SuccessorDecisions.Any(item => item.TemplateVersionId == proposal.Version.VersionId) &&
                next.SuccessorDecisions.Count == prior.SuccessorDecisions.Count + 1 &&
                next.SuccessorDecisions[^1].TemplateVersionId == proposal.Version.VersionId &&
                next.SuccessorDecisions[^1].DecidedByMemberId == actor && next.SuccessorDecisions[^1].DecidedAt == next.UpdatedAt,
            _ => false,
        };
    }

    static bool DraftsCompatible(IReadOnlyList<TemplateApplicationDraftView> prior,
        IReadOnlyList<TemplateApplicationDraftView> next, string transition)
    {
        if (prior.Count != next.Count)
            return false;
        for (var index = 0; index < prior.Count; index++)
        {
            var before = prior[index];
            var after = next[index];
            if (before.EntryKey != after.EntryKey || before.RecordKind != after.RecordKind ||
                before.PlannedRecordId != after.PlannedRecordId)
                return false;
            if (transition == "draft_created" && before.Status == "draft_pending" &&
                after.Status == "draft_created" && before.CreatedRecordId is null)
                continue;
            if (Digest(before) != Digest(after))
                return false;
        }
        return true;
    }

    CommandFailure? ValidateState(TemplateApplicationView state)
    {
        var source = NormalizePublishedVersion(state.SourceVersion);
        if (state.TenantId != _tenantId || state.ProgramId != _programId || state.TemplateApplicationId != Id ||
            state.Revision <= 0 || source is null || state.TemplateId != source.TemplateId ||
            state.TemplateVersionId != source.VersionId || state.TemplateVersionNumber != source.VersionNumber ||
            state.TemplateContentSha256 != source.ContentSha256 || state.Drafts is null ||
            state.Drafts.Count != source.Content.Records.Count || state.Drafts.Count > MaximumDrafts ||
            state.Drafts.Where((draft, index) => draft.EntryKey != source.Content.Records[index].EntryKey ||
                draft.RecordKind != source.Content.Records[index].RecordKind ||
                draft.PlannedRecordId != Uuid.CreateVersion5(Id, "template-entry:" + draft.EntryKey) ||
                !ValidDraft(draft)).Any() || state.Drafts.Select(item => item.EntryKey).Distinct(StringComparer.Ordinal).Count() != state.Drafts.Count ||
            !Token(state.ApplicationKey, 200) || state.IsActivated ||
            state.Status != (state.Drafts.All(item => item.Status == "draft_created") ? "drafts_created" : "applied") ||
            state.SuccessorProposals is null || state.SuccessorProposals.Count > MaximumSuccessorDecisions ||
            state.SuccessorProposals.Select((proposal, index) => !ValidProposal(proposal, state) ||
                index > 0 && proposal.Version.VersionNumber <= state.SuccessorProposals[index - 1].Version.VersionNumber)
                .Any(item => item) ||
            state.SuccessorProposals.Select(item => item.Version.VersionId).Distinct().Count() != state.SuccessorProposals.Count ||
            state.SuccessorDecisions is null || state.SuccessorDecisions.Count > MaximumSuccessorDecisions ||
            state.SuccessorDecisions.Select(item => item.TemplateVersionId).Distinct().Count() != state.SuccessorDecisions.Count ||
            state.SuccessorDecisions.Any(item => !ValidSuccessorDecision(item, state)) ||
            !ValidActor(state.RequestedByMemberId, state.RequestedByDisplay, state.RequestedAt) ||
            !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt) ||
            state.ContentSha256 != DigestState(state with { ContentSha256 = string.Empty }))
            return Invalid("The template-application snapshot is invalid or does not retain exact published provenance.");
        return null;
    }

    static bool ValidProposal(TemplateApplicationSuccessorProposalView proposal,
        TemplateApplicationView state) => proposal is not null &&
        NormalizePublishedVersion(proposal.Version) is { } version && version.TemplateId == state.TemplateId &&
        version.VersionNumber > state.TemplateVersionNumber && proposal.ContentSha256 == version.ContentSha256 &&
        proposal.ProposedDisposition is "adopt" or "adapt" or "decline" &&
        ValidActor(proposal.ProposedByMemberId, proposal.ProposedByDisplay, proposal.ProposedAt) &&
        proposal.ProposedAt >= state.RequestedAt;

    static bool ValidSuccessorDecision(TemplateApplicationSuccessorDecisionView item,
        TemplateApplicationView state) => item is not null &&
        state.SuccessorProposals.Any(proposal => proposal.Version.VersionId == item.TemplateVersionId &&
            proposal.Version.VersionNumber == item.TemplateVersionNumber) &&
        item.Disposition is "adopt" or "adapt" or "decline" && Bounded(item.Rationale, 1, MaximumTextLength) &&
        ValidActor(item.DecidedByMemberId, item.DecidedByDisplay, item.DecidedAt);

    static bool ValidDraft(TemplateApplicationDraftView item) => item is not null &&
        item.Status is "draft_pending" or "draft_created" &&
        (item.Status == "draft_pending"
            ? item.CreatedRecordId is null && item.CreatedRecordVersion is null && item.CreatedRecordSha256 is null
            : item.CreatedRecordId == item.PlannedRecordId && item.CreatedRecordVersion > 0 && Sha256(item.CreatedRecordSha256));

    static bool ValidInitialState(TemplateApplicationView state) => state.Revision == 1 &&
        state.Status == "applied" && state.Drafts.Count > 0 && state.Drafts.All(item => item.Status == "draft_pending") &&
        state.SuccessorProposals.Count == 0 && state.SuccessorDecisions.Count == 0 && !state.IsActivated;

    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;

    static bool SameApplication(TemplateApplicationView state, MethodologyTemplateVersionView? version,
        string key, Uuid actor, string display, DateTimeOffset at) =>
        NormalizePublishedVersion(version) is { } normalized && normalized.TemplateId == state.TemplateId &&
        normalized.VersionId == state.TemplateVersionId && normalized.ContentSha256 == state.TemplateContentSha256 &&
        Clean(key) == state.ApplicationKey && actor == state.RequestedByMemberId &&
        Clean(display) == state.RequestedByDisplay && at == state.RequestedAt;

    static MethodologyTemplateVersionView? NormalizePublishedVersion(MethodologyTemplateVersionView? version)
    {
        if (version is null || version.TemplateId == Uuid.Empty || version.VersionId == Uuid.Empty ||
            version.VersionNumber <= 0 || version.Status != "published" || version.Content is null ||
            !Sha256(version.ContentSha256) || version.Review is not { Outcome: "approved" } review ||
            version.ApprovedByMemberId is not { } approver || approver == version.AuthoredByMemberId ||
            approver == review.ReviewedByMemberId || version.ApprovedAt is not { } approvedAt ||
            approvedAt < review.ReviewedAt || !Bounded(version.ApprovalRationale, 1, MaximumTextLength) ||
            !ValidActor(approver, version.ApprovedByDisplay, approvedAt) ||
            !ValidActor(version.AuthoredByMemberId, version.AuthoredByDisplay, version.AuthoredAt) ||
            !ValidActor(review.ReviewedByMemberId, review.ReviewedByDisplay, review.ReviewedAt) ||
            !Bounded(version.ChangeNotes, 1, MaximumTextLength))
            return null;
        var content = NormalizeContent(version.Content);
        return content is not null && Digest(content) == version.ContentSha256
            && ValidContributionProvenance(version, content)
            ? version with
            {
                Content = content,
                ContentSha256 = NormalizeHash(version.ContentSha256),
                AcceptedContributions = Freeze((version.AcceptedContributions ?? []).ToArray()),
            }
            : null;
    }

    static bool ValidContributionProvenance(MethodologyTemplateVersionView version,
        MethodologyTemplateContent content)
    {
        var approvals = version.AcceptedContributions ?? [];
        return approvals.Select(item => item.AcceptanceId).Distinct().Count() == approvals.Count &&
            approvals.All(item => item.AcceptanceId != Uuid.Empty && Sha256(item.ContentSha256) &&
                ValidActor(item.ReviewedByMemberId, item.ReviewedByDisplay, item.ReviewedAt) &&
                item.AcceptedByMemberId != Uuid.Empty && item.AcceptedByMemberId != item.ReviewedByMemberId &&
                ValidActor(item.AcceptedByMemberId, item.AcceptedByDisplay, item.AcceptedAt) &&
                Bounded(item.AcceptanceRationale, 1, MaximumTextLength) && item.AcceptedAt >= item.ReviewedAt) &&
            content.Records.All(record => record.ContributionApprovalId is null || approvals.Any(item =>
                item.AcceptanceId == record.ContributionApprovalId && item.ContentSha256 == Digest(record)));
    }

    static MethodologyTemplateContent? NormalizeContent(MethodologyTemplateContent? content)
    {
        if (content is null || !Bounded(content.Name, 1, 200) || !Bounded(content.Description, 1, MaximumTextLength) ||
            content.Records is null || content.Records.Count is < 1 or > MaximumDrafts)
            return null;
        var kind = Clean(content.TemplateKind).ToLowerInvariant();
        if (kind is not ("soc2" or "iso27001" or "custom") ||
            content.Records.Any(item => item is null || !Token(item.EntryKey, 100) ||
                item.RecordKind is not ("control" or "policy" or "evidence_request" or "risk") ||
                !Bounded(item.Title, 1, 200) || !Bounded(item.Summary, 1, MaximumTextLength) ||
                item.CriteriaReferences is null || item.CriteriaReferences.Count > 50 ||
                item.CriteriaReferences.Any(reference => reference is null || reference.EditionId == Uuid.Empty ||
                    !Token(reference.Identifier, 100))) ||
            content.Records.Select(item => item.EntryKey).Distinct(StringComparer.Ordinal).Count() != content.Records.Count)
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

    static TemplateApplicationView State(TemplateApplicationView current, string status,
        IReadOnlyList<TemplateApplicationDraftView> drafts,
        IReadOnlyList<TemplateApplicationSuccessorProposalView> proposals,
        IReadOnlyList<TemplateApplicationSuccessorDecisionView> decisions,
        Uuid actor, string display, DateTimeOffset at)
    {
        var state = current with
        {
            Status = status,
            Revision = current.Revision + 1,
            Drafts = Freeze(drafts.ToArray()),
            SuccessorProposals = Freeze(proposals.Select(proposal => proposal with
            {
                Version = FreezeVersion(proposal.Version),
            }).ToArray()),
            SuccessorDecisions = Freeze(decisions.ToArray()),
            ActorMemberId = actor,
            ActorDisplay = display,
            UpdatedAt = at,
            ContentSha256 = string.Empty,
        };
        return state with { ContentSha256 = DigestState(state) };
    }

    static TemplateApplicationView Freeze(TemplateApplicationView state) => state with
    {
        SourceVersion = FreezeVersion(state.SourceVersion),
        Drafts = Freeze(state.Drafts.ToArray()),
        SuccessorProposals = Freeze(state.SuccessorProposals.Select(proposal => proposal with
        {
            Version = FreezeVersion(proposal.Version),
        }).ToArray()),
        SuccessorDecisions = Freeze(state.SuccessorDecisions.ToArray()),
    };

    static MethodologyTemplateVersionView FreezeVersion(MethodologyTemplateVersionView version) => version with
    {
        Content = version.Content with
        {
            Records = Freeze(version.Content.Records.Select(item => item with
            {
                CriteriaReferences = Freeze(item.CriteriaReferences.ToArray()),
            }).ToArray()),
        },
        AcceptedContributions = Freeze((version.AcceptedContributions ?? []).ToArray()),
    };

    static string DigestState(TemplateApplicationView state) => Digest(new
    {
        state.TenantId,
        state.ProgramId,
        state.TemplateApplicationId,
        state.Status,
        state.Revision,
        state.TemplateId,
        state.TemplateVersionId,
        state.TemplateVersionNumber,
        state.TemplateContentSha256,
        state.ApplicationKey,
        state.SourceVersion,
        state.Drafts,
        state.SuccessorProposals,
        state.SuccessorDecisions,
        state.IsActivated,
        state.RequestedByMemberId,
        state.RequestedByDisplay,
        state.RequestedAt,
        state.ActorMemberId,
        state.ActorDisplay,
        state.UpdatedAt,
    });

    static bool Prefix<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) => prior.Count <= next.Count &&
        prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));
    static TemplateApplicationSuccessorProposalView? LastProposal(
        IReadOnlyList<TemplateApplicationSuccessorProposalView> proposals) =>
        proposals.Count == 0 ? null : proposals[^1];
    static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum && !value.Any(char.IsControl);
    static bool Token(string? value, int max) => Bounded(value, 1, max) && value!.Trim().All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty && Bounded(display, 1, 200) && at != default;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("template application", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T item) => Freeze(values.Append(item).ToArray());
}
