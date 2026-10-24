using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Owns one tenant's de-identified contribution, independent review, and explicit platform acceptance.</summary>
public sealed class MethodologyTemplateContribution : Aggregate
{
    public NativeAcceptance ReplayAcceptance { get; }
    OperationProvenance? _pendingOperation;

    const int MaximumTextLength = 2000;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    static readonly HashSet<string> RecordKinds = new(StringComparer.Ordinal)
        { "control", "policy", "evidence_request", "risk" };

    readonly Uuid _tenantId;
    MethodologyTemplateContributionView? _current;

    public long Revision => _current?.Revision ?? 0;
    public MethodologyTemplateContributionView? Current => _current;
    public MethodologyTemplateContributionShareView? ShareableContent =>
        _current is
        {
            Status: "accepted", AcceptanceId: { } acceptanceId,
            Review: { Outcome: "approved" } review, AcceptedByMemberId: { } acceptedBy,
            AcceptedByDisplay: { } acceptedDisplay, AcceptanceRationale: { } rationale,
            AcceptedAt: { } acceptedAt
        } current
            ? new MethodologyTemplateContributionShareView(acceptanceId,
                FreezeContent(current.ProposedContent with { ContributionApprovalId = acceptanceId }),
                Digest(current.ProposedContent with { ContributionApprovalId = acceptanceId }),
                review, acceptedBy, acceptedDisplay, rationale, acceptedAt)
            : null;

    public MethodologyTemplateContribution(Uuid tenantId, Uuid contributionId, Uuid selectedCommandId = default)
        : base(contributionId, new EventStreamAddress(tenantId.ToString(), "methodology-template-contributions",
            contributionId.ToString()))
    {
        ReplayAcceptance = new NativeAcceptance(selectedCommandId);
        _tenantId = tenantId;
        On<MethodologyTemplateContributionProposed>(Apply);
        On<MethodologyTemplateContributionReviewed>(Apply);
        On<MethodologyTemplateContributionAccepted>(Apply);
    }

    public CommandFailure? Propose(Uuid sourceRecordId, long sourceRecordVersion,
        MethodologyTemplateRecordContent proposedContent, string deidentificationRationale,
        Uuid contributorMemberId, string contributorDisplay, DateTimeOffset proposedAt)
    {
        deidentificationRationale = Clean(deidentificationRationale);
        if (_current is { } existing)
            return SameProposal(existing, sourceRecordId, sourceRecordVersion, proposedContent,
                deidentificationRationale, contributorMemberId, contributorDisplay, proposedAt)
                ? null : CommandFailure.StateConflict("A template contribution identity cannot be reused for different source content.");
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || sourceRecordId == Uuid.Empty ||
            sourceRecordVersion <= 0 || !ValidActor(contributorMemberId, contributorDisplay, proposedAt) ||
            !Bounded(deidentificationRationale, 1, MaximumTextLength) ||
            NormalizeContent(proposedContent) is not { } normalized ||
            ContainsSourceIdentity(normalized, _tenantId, sourceRecordId))
            return Invalid("A reusable contribution must be bounded, attributed, and de-identified from its tenant and source record.");
        var state = new MethodologyTemplateContributionView(_tenantId, Id, "proposed", 1,
            sourceRecordId, sourceRecordVersion, normalized, deidentificationRationale,
            contributorMemberId, contributorDisplay.Trim(), proposedAt, null, null, null, null,
            null, null, contributorMemberId, contributorDisplay.Trim(), proposedAt, string.Empty);
        state = state with { ContentSha256 = StateDigest(state) };
        return Emit(state, contributorMemberId, proposedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateContributionProposed(tenant, subject, revision, actor, snapshot, at),
            "propose", true);
    }

    public CommandFailure? Review(long expectedRevision, string outcome, string comments,
        Uuid reviewerMemberId, string reviewerDisplay, DateTimeOffset reviewedAt,
        OperationProvenance? operation = null)
    {
        if (AuthoredCommandReplay.TryReplay(ReplayAcceptance, operation, out var replayFailure))
            return replayFailure;
        _pendingOperation = operation;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The template contribution was not found.");
        outcome = Clean(outcome).ToLowerInvariant();
        comments = Clean(comments);
        if (current.Review is { } previous)
            return previous.Outcome == outcome && previous.Comments == comments &&
                previous.ReviewedByMemberId == reviewerMemberId && previous.ReviewedByDisplay == Clean(reviewerDisplay) &&
                previous.ReviewedAt == reviewedAt
                ? null : CommandFailure.StateConflict("A template contribution review decision is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "proposed" || outcome is not ("approved" or "declined") ||
            !Bounded(comments, 1, MaximumTextLength) || !ValidActor(reviewerMemberId, reviewerDisplay, reviewedAt) ||
            reviewedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("A contribution review requires a proposed item and explicit bounded review notes.");
        if (reviewerMemberId == current.ProposedByMemberId)
            return CommandFailure.ActorProhibited("A contribution author cannot review their own de-identification.");
        var review = new MethodologyTemplateReviewView(Uuid.CreateVersion4(), outcome, comments,
            reviewerMemberId, reviewerDisplay.Trim(), reviewedAt);
        var state = State(current, outcome == "approved" ? "reviewed" : "declined", review,
            null, null, null, null, null, reviewerMemberId, reviewerDisplay.Trim(), reviewedAt);
        return Emit(state, reviewerMemberId, reviewedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateContributionReviewed(tenant, subject, revision, actor, snapshot, at), "review");
    }

    public CommandFailure? Accept(long expectedRevision, string acceptanceRationale,
        Uuid platformOperatorId, string operatorDisplay, DateTimeOffset acceptedAt,
        OperationProvenance? operation = null)
    {
        if (AuthoredCommandReplay.TryReplay(ReplayAcceptance, operation, out var replayFailure))
            return replayFailure;
        _pendingOperation = operation;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The template contribution was not found.");
        acceptanceRationale = Clean(acceptanceRationale);
        if (current.Status == "accepted")
            return current.AcceptanceRationale == acceptanceRationale && current.AcceptedByMemberId == platformOperatorId &&
                current.AcceptedByDisplay == Clean(operatorDisplay) && current.AcceptedAt == acceptedAt
                ? null : CommandFailure.StateConflict("An accepted template contribution cannot be changed.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "reviewed" || current.Review is not { Outcome: "approved" } review ||
            !Bounded(acceptanceRationale, 1, MaximumTextLength) ||
            !ValidActor(platformOperatorId, operatorDisplay, acceptedAt) || acceptedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("Only an independently reviewed, approved contribution can be accepted.");
        if (platformOperatorId == current.ProposedByMemberId || platformOperatorId == review.ReviewedByMemberId)
            return CommandFailure.ActorProhibited("The contributor and reviewer cannot accept a contribution into platform methodology.");
        var state = State(current, "accepted", review, Uuid.CreateVersion4(), acceptanceRationale,
            platformOperatorId, operatorDisplay.Trim(), acceptedAt, platformOperatorId, operatorDisplay.Trim(), acceptedAt);
        return Emit(state, platformOperatorId, acceptedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new MethodologyTemplateContributionAccepted(tenant, subject, revision, actor, snapshot, at), "accept");
    }

    void Apply(MethodologyTemplateContributionProposed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "propose");
    void Apply(MethodologyTemplateContributionReviewed ev)
    {
        AuthoredCommandReplay.Observe(this, ReplayAcceptance, ev, ev.Operation);
        ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "review");
    }
    void Apply(MethodologyTemplateContributionAccepted ev)
    {
        AuthoredCommandReplay.Observe(this, ReplayAcceptance, ev, ev.Operation);
        ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "accept");
    }

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        MethodologyTemplateContributionView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null || state.TenantId != _tenantId ||
            state.ContributionId != Id || state.Revision != revision || state.UpdatedAt != occurredAt ||
            revision != Revision + 1 || creating != (_current is null) || actorMemberId == Uuid.Empty ||
            state.ActorMemberId != actorMemberId || !ValidState(state) ||
            _current is null && (transition != "propose" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The methodology-template contribution event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(MethodologyTemplateContributionView state, Uuid actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, MethodologyTemplateContributionView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (!ValidState(state) ||
            (creating ? _current is not null : _current is null || !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The methodology-template contribution transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        ev = ev switch
        {
            MethodologyTemplateContributionReviewed fact => fact with { Operation = _pendingOperation },
            MethodologyTemplateContributionAccepted fact => fact with { Operation = _pendingOperation },
            _ => ev,
        };
        if (!Fits(ev))
            return Invalid("The methodology-template contribution event exceeds the bounded payload.");
        RaiseEvent(ev);
        return null;
    }

    bool ValidState(MethodologyTemplateContributionView state)
    {
        var normalized = NormalizeContent(state.ProposedContent);
        if (state.TenantId != _tenantId || state.ContributionId != Id || state.Revision <= 0 ||
            state.SourceRecordId == Uuid.Empty || state.SourceRecordVersion <= 0 || normalized is null ||
            ContainsSourceIdentity(normalized, _tenantId, state.SourceRecordId) ||
            Digest(normalized) != Digest(state.ProposedContent) ||
            !Bounded(state.DeidentificationRationale, 1, MaximumTextLength) ||
            !ValidActor(state.ProposedByMemberId, state.ProposedByDisplay, state.ProposedAt) ||
            !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt) ||
            state.Status is not ("proposed" or "reviewed" or "declined" or "accepted") ||
            state.ContentSha256 != StateDigest(state))
            return false;
        if (state.Review is { } review &&
            (review.ReviewId == Uuid.Empty || review.Outcome is not ("approved" or "declined") ||
             !Bounded(review.Comments, 1, MaximumTextLength) ||
             !ValidActor(review.ReviewedByMemberId, review.ReviewedByDisplay, review.ReviewedAt) ||
             review.ReviewedByMemberId == state.ProposedByMemberId || review.ReviewedAt < state.ProposedAt))
            return false;
        if (state.Status == "proposed" && state.Review is not null ||
            state.Status == "reviewed" && state.Review?.Outcome != "approved" ||
            state.Status == "declined" && state.Review?.Outcome != "declined")
            return false;
        return state.Status == "accepted"
            ? state.Review?.Outcome == "approved" && state.AcceptanceId is { } acceptanceId && acceptanceId != Uuid.Empty &&
              Bounded(state.AcceptanceRationale, 1, MaximumTextLength) &&
              state.AcceptedByMemberId is { } acceptedBy && acceptedBy != state.ProposedByMemberId &&
              acceptedBy != state.Review.ReviewedByMemberId &&
              state.AcceptedByDisplay is { } acceptedDisplay && Bounded(acceptedDisplay, 1, 200) &&
              state.AcceptedAt is { } acceptedAt && acceptedAt >= state.Review.ReviewedAt &&
              acceptedAt == state.UpdatedAt && state.ActorMemberId == acceptedBy
            : state.AcceptanceId is null && state.AcceptanceRationale is null &&
              state.AcceptedByMemberId is null && state.AcceptedByDisplay is null && state.AcceptedAt is null;
    }

    static bool ValidTransition(MethodologyTemplateContributionView prior, MethodologyTemplateContributionView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ContributionId != prior.ContributionId || next.SourceRecordId != prior.SourceRecordId ||
            next.UpdatedAt < prior.UpdatedAt ||
            next.SourceRecordVersion != prior.SourceRecordVersion ||
            Digest(next.ProposedContent) != Digest(prior.ProposedContent) ||
            next.DeidentificationRationale != prior.DeidentificationRationale ||
            next.ProposedByMemberId != prior.ProposedByMemberId || next.ProposedAt != prior.ProposedAt ||
            next.ActorMemberId != actor)
            return false;
        return transition switch
        {
            "review" => prior.Status == "proposed" && next.Status is "reviewed" or "declined" &&
                next.Review is { } review && review.ReviewedByMemberId == actor && review.ReviewedAt == next.UpdatedAt,
            "accept" => prior.Status == "reviewed" && next.Status == "accepted" &&
                Digest(next.Review) == Digest(prior.Review) && next.AcceptanceId is not null &&
                next.AcceptedByMemberId == actor && next.AcceptedAt == next.UpdatedAt,
            _ => false,
        };
    }

    static bool ValidInitialState(MethodologyTemplateContributionView state) =>
        state.Revision == 1 && state.Status == "proposed" && state.Review is null &&
        state.AcceptanceId is null && state.ActorMemberId == state.ProposedByMemberId &&
        state.UpdatedAt == state.ProposedAt;

    static bool SameProposal(MethodologyTemplateContributionView state, Uuid sourceRecordId,
        long sourceRecordVersion, MethodologyTemplateRecordContent content, string rationale,
        Uuid actor, string display, DateTimeOffset at) =>
        NormalizeContent(content) is { } normalized && state.SourceRecordId == sourceRecordId &&
        state.SourceRecordVersion == sourceRecordVersion && Digest(state.ProposedContent) == Digest(normalized) &&
        state.DeidentificationRationale == rationale && state.ProposedByMemberId == actor;

    static bool ContainsSourceIdentity(MethodologyTemplateRecordContent content, Uuid tenantId, Uuid sourceRecordId)
    {
        var serialized = JsonSerializer.Serialize(content);
        return serialized.Contains(tenantId.ToString(), StringComparison.OrdinalIgnoreCase) ||
            serialized.Contains(sourceRecordId.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    static MethodologyTemplateRecordContent? NormalizeContent(MethodologyTemplateRecordContent? content)
    {
        if (content is null || !Token(content.EntryKey, 100) ||
            !RecordKinds.Contains(Clean(content.RecordKind).ToLowerInvariant()) ||
            !Bounded(content.Title, 1, 200) || !Bounded(content.Summary, 1, MaximumTextLength) ||
            content.CriteriaReferences is null || content.CriteriaReferences.Count > 50 ||
            content.CriteriaReferences.Any(item => item is null || item.EditionId == Uuid.Empty || !Token(item.Identifier, 100)) ||
            content.ContributionApprovalId is not null)
            return null;
        return FreezeContent(content with
        {
            EntryKey = content.EntryKey.Trim(),
            RecordKind = content.RecordKind.Trim().ToLowerInvariant(),
            Title = content.Title.Trim(),
            Summary = content.Summary.Trim(),
            CriteriaReferences = Freeze(content.CriteriaReferences.Select(item => item with
            { Identifier = item.Identifier.Trim().ToUpperInvariant() })
                .OrderBy(item => item.EditionId.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.Identifier, StringComparer.Ordinal).ToArray()),
        });
    }

    static MethodologyTemplateContributionView State(MethodologyTemplateContributionView current, string status,
        MethodologyTemplateReviewView? review, Uuid? acceptanceId, string? acceptanceRationale,
        Uuid? acceptedBy, string? acceptedDisplay, DateTimeOffset? acceptedAt,
        Uuid actor, string display, DateTimeOffset at)
    {
        var state = current with
        {
            Status = status,
            Revision = current.Revision + 1,
            Review = review,
            AcceptanceId = acceptanceId,
            AcceptanceRationale = acceptanceRationale,
            AcceptedByMemberId = acceptedBy,
            AcceptedByDisplay = acceptedDisplay,
            AcceptedAt = acceptedAt,
            ActorMemberId = actor,
            ActorDisplay = display,
            UpdatedAt = at,
            ContentSha256 = string.Empty,
        };
        return state with { ContentSha256 = StateDigest(state) };
    }

    static MethodologyTemplateContributionView Freeze(MethodologyTemplateContributionView state) => state with
    { ProposedContent = FreezeContent(state.ProposedContent) };
    static MethodologyTemplateRecordContent FreezeContent(MethodologyTemplateRecordContent content) => content with
    { CriteriaReferences = Freeze(content.CriteriaReferences.ToArray()) };
    static string StateDigest(MethodologyTemplateContributionView state) => Digest(new
    {
        state.TenantId,
        state.ContributionId,
        state.Status,
        state.Revision,
        state.SourceRecordId,
        state.SourceRecordVersion,
        state.ProposedContent,
        state.DeidentificationRationale,
        state.ProposedByMemberId,
        state.ProposedByDisplay,
        state.ProposedAt,
        state.Review,
        state.AcceptanceId,
        state.AcceptanceRationale,
        state.AcceptedByMemberId,
        state.AcceptedByDisplay,
        state.AcceptedAt,
        state.ActorMemberId,
        state.ActorDisplay,
        state.UpdatedAt,
    });
    static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static bool Fits(DomainEvent value) => DomainEventPayload.Size(value) <= MaximumEventBytes;
    static bool Bounded(string? value, int min, int max) => value is not null && value.Trim().Length >= min &&
        value.Trim().Length <= max && !value.Any(char.IsControl);
    static bool Token(string? value, int max) => Bounded(value, 1, max) && value!.Trim().All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty && Bounded(display, 1, 200) && at != default;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("template contribution", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
}
