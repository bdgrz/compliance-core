using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Commands;
using Bdgrz.Compliance.Features.Commitments.Governance.Events;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;

/// <summary>Owns the immutable identifier, one open draft and one-step publication of that commitment.</summary>
public sealed class Commitment : Aggregate
{
    public const string Area = "commitments";
    readonly Dictionary<Uuid, (string Intent, Uuid Actor)> _accepted = [];
    readonly HashSet<Uuid> _authors = [];
    readonly List<ApprovedCommitment> _versions = [];
    public Commitment(Uuid tenantId, Uuid commitmentId) : base(commitmentId,
        new EventStreamAddress(tenantId.ToString(), Area, commitmentId.ToString()))
    {
        TenantId = tenantId;
        On<CommitmentDrafted>(Apply);
        On<CommitmentChangeProposed>(Apply);
        On<CommitmentDraftRevised>(Apply);
        On<CommitmentDraftDiscarded>(Apply);
        On<CommitmentChangesRequested>(Apply);
        On<CommitmentVersionApproved>(Apply);
        On<CommitmentRetired>(Apply);
    }
    public Uuid TenantId { get; }
    public Uuid ProgramId { get; private set; }
    public string Kind { get; private set; } = "";
    public string Identifier { get; private set; } = "";
    public long Revision { get; private set; }
    public CommitmentTerms? OpenDraft { get; private set; }
    public DateOnly? RetiredOn { get; private set; }
    public IReadOnlyList<ApprovedCommitment> Versions => _versions.AsReadOnly();
    public bool IsAuthor(Uuid member) => _authors.Contains(member);
    public bool IsActiveOn(DateOnly date) => _versions.Any(value => value.EffectiveOn <= date) &&
        (RetiredOn is null || RetiredOn > date);
    public static Uuid IdFor(Uuid tenant, Uuid program, string kind, string identifier) =>
        Bdgrz.Compliance.Features.Commitments.Aggregates.CommitmentDraft.IdFor(tenant, program, kind, identifier);
    public bool HasAccepted(Uuid command) => _accepted.ContainsKey(command);
    public CommandFailure? Draft(DraftCommitment request, ActorReference actor, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (Retry(request.CommitmentId, digest, actor, out var prior))
            return prior;
        var kind = request.Kind?.Trim().ToLowerInvariant() ?? "";
        var identifier = request.Identifier?.Trim().ToUpperInvariant() ?? "";
        if (Revision != 0 || request.TenantId != TenantId || request.ProgramId == Uuid.Empty ||
            request.CommitmentId != Id || Id != IdFor(TenantId, request.ProgramId, kind, identifier) ||
            !Kinds.Contains(kind) || !Text(identifier, 64) || !ValidTerms(kind, request.Terms) || !Actor(actor) || at == default)
            return Invalid();
        RaiseEvent(new CommitmentDrafted(TenantId, request.ProgramId, Id, 1, request.CommitmentId,
            digest, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), at, kind, identifier, Freeze(request.Terms)));
        return null;
    }
    public CommandFailure? Propose(ProposeCommitmentChange request, ActorReference actor, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (Retry(request.CommandId, digest, actor, out var prior))
            return prior;
        var guard = Guard(request.TenantId, request.ProgramId, request.CommitmentId, request.CommandId,
            request.ExpectedRevision, actor, at);
        if (guard is not null)
            return guard;
        if (OpenDraft is not null || _versions.Count == 0 || !ValidTerms(Kind, request.Terms))
            return Invalid();
        RaiseEvent(new CommitmentChangeProposed(TenantId, ProgramId, Id, Revision + 1, request.CommandId,
            digest, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), at, Freeze(request.Terms)));
        return null;
    }
    public CommandFailure? Revise(ReviseCommitmentDraft request, ActorReference actor, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (Retry(request.CommandId, digest, actor, out var prior))
            return prior;
        var guard = Guard(request.TenantId, request.ProgramId, request.CommitmentId, request.CommandId,
            request.ExpectedRevision, actor, at);
        if (guard is not null)
            return guard;
        if (OpenDraft is null || !ValidTerms(Kind, request.Terms))
            return Invalid();
        RaiseEvent(new CommitmentDraftRevised(TenantId, ProgramId, Id, Revision + 1, request.CommandId,
            digest, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), at, Freeze(request.Terms)));
        return null;
    }
    public CommandFailure? Discard(DiscardCommitmentDraft request, ActorReference actor, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (Retry(request.CommandId, digest, actor, out var prior))
            return prior;
        var guard = Guard(request.TenantId, request.ProgramId, request.CommitmentId, request.CommandId,
            request.ExpectedRevision, actor, at);
        if (guard is not null)
            return guard;
        if (OpenDraft is null)
            return Invalid();
        RaiseEvent(new CommitmentDraftDiscarded(TenantId, ProgramId, Id, Revision + 1, request.CommandId,
            digest, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), at));
        return null;
    }
    public CommandFailure? Review(ReviewCommitmentDraft request, ActorReference actor, Uuid boundaryVersion,
        SeparationOfDutiesWaiverProof? waiver, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (Retry(request.CommandId, digest, actor, out var prior))
            return prior;
        var guard = Guard(request.TenantId, request.ProgramId, request.CommitmentId, request.CommandId,
            request.ExpectedRevision, actor, at);
        if (guard is not null)
            return guard;
        if (OpenDraft is not { } terms || request.Outcome is not ("approve" or "changes_requested") ||
            !Text(request.Rationale, 2000))
            return Invalid();
        var member = Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture);
        if (_authors.Contains(member) && (waiver is null || !CommitmentWaiverProof.Allows(waiver, TenantId,
            new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Commitment, Id, Id, Revision,
                SeparationOfDutiesActions.Approve), member, at)))
            return CommandFailure.StateConflict("A draft author cannot approve or request changes without an exact waiver.");
        if (request.Outcome == "changes_requested")
        {
            RaiseEvent(new CommitmentChangesRequested(TenantId, ProgramId, Id, Revision + 1, request.CommandId,
                digest, member, at, request.Rationale.Trim()));
            return null;
        }
        if (boundaryVersion == Uuid.Empty || request.EffectiveOn is not { } effective || effective == default ||
            _versions.Count > 0 && effective <= _versions[^1].EffectiveOn)
            return Invalid();
        var approved = new ApprovedCommitment(_versions.Count + 1, Revision, terms, effective, boundaryVersion,
            member, at, waiver?.WaiverId);
        RaiseEvent(new CommitmentVersionApproved(TenantId, ProgramId, Id, Revision + 1, request.CommandId,
            digest, member, at, approved, waiver));
        return null;
    }
    public CommandFailure? Retire(RetireCommitment request, ActorReference actor, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (Retry(request.CommandId, digest, actor, out var prior))
            return prior;
        var guard = Guard(request.TenantId, request.ProgramId, request.CommitmentId, request.CommandId,
            request.ExpectedRevision, actor, at);
        if (guard is not null)
            return guard;
        if (_versions.Count == 0 || request.EffectiveOn <= _versions[^1].EffectiveOn || !Text(request.Reason, 2000))
            return Invalid();
        RaiseEvent(new CommitmentRetired(TenantId, ProgramId, Id, Revision + 1, request.CommandId, digest,
            Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), at, request.EffectiveOn, request.Reason.Trim()));
        return null;
    }
    CommandFailure? Guard(Uuid tenant, Uuid program, Uuid commitment, Uuid command, long revision,
        ActorReference actor, DateTimeOffset at) => Revision == 0 || tenant != TenantId || program != ProgramId || commitment != Id
        ? CommandFailure.MissingRecord("The commitment was not found in this program.")
        : command == Uuid.Empty || revision != Revision || RetiredOn is not null || !Actor(actor) || at == default
            ? CommandFailure.StateConflict("The commitment revision or lifecycle does not admit this command.") : null;
    bool Retry(Uuid command, string digest, ActorReference actor, out CommandFailure? failure)
    {
        failure = null;
        if (!_accepted.TryGetValue(command, out var prior))
            return false;
        if (!Actor(actor) || prior != (digest, Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture)))
            failure = CommandFailure.StateConflict("The command identity already accepted another actor or intent.");
        return true;
    }
    void Apply(CommitmentGovernanceFact fact)
    {
        if (fact.TenantId != TenantId || fact.CommitmentId != Id || fact.Revision != Revision + 1 ||
            fact.CommandId == Uuid.Empty || fact.ActorId == Uuid.Empty || fact.OccurredAt == default ||
            fact.IntentSha256.Length != 64 || _accepted.ContainsKey(fact.CommandId) ||
            Revision > 0 && (fact.ProgramId != ProgramId || RetiredOn is not null))
            throw new InvalidOperationException("The commitment fact violates its scope or lifecycle.");
        switch (fact)
        {
            case CommitmentDrafted value when Revision == 0 && Kinds.Contains(value.Kind) &&
                Id == IdFor(TenantId, value.ProgramId, value.Kind, value.Identifier) && ValidTerms(value.Kind, value.Terms):
                ProgramId = value.ProgramId; Kind = value.Kind; Identifier = value.Identifier;
                OpenDraft = Freeze(value.Terms); _authors.Add(value.ActorId); break;
            case CommitmentChangeProposed value when OpenDraft is null && _versions.Count > 0 && ValidTerms(Kind, value.Terms):
                OpenDraft = Freeze(value.Terms); _authors.Clear(); _authors.Add(value.ActorId); break;
            case CommitmentDraftRevised value when OpenDraft is not null && ValidTerms(Kind, value.Terms):
                OpenDraft = Freeze(value.Terms); _authors.Add(value.ActorId); break;
            case CommitmentDraftDiscarded when OpenDraft is not null:
                OpenDraft = null; _authors.Clear(); break;
            case CommitmentChangesRequested value when OpenDraft is not null && Text(value.Rationale, 2000):
                break;
            case CommitmentVersionApproved value when OpenDraft is not null && value.Approved.Version == _versions.Count + 1 &&
                value.Approved.DraftRevision == Revision && value.Approved.BoundaryVersionId != Uuid.Empty &&
                value.Approved.ApprovedBy == value.ActorId && value.Approved.ApprovedAt == value.OccurredAt &&
                (_versions.Count == 0 || value.Approved.EffectiveOn > _versions[^1].EffectiveOn) &&
                ContentDigest.OfJson(value.Approved.Terms) == ContentDigest.OfJson(OpenDraft):
                _versions.Add(value.Approved with { Terms = Freeze(value.Approved.Terms) }); OpenDraft = null; _authors.Clear(); break;
            case CommitmentRetired value when _versions.Count > 0 && value.EffectiveOn > _versions[^1].EffectiveOn && Text(value.Reason, 2000):
                RetiredOn = value.EffectiveOn; OpenDraft = null; _authors.Clear(); break;
            default: throw new InvalidOperationException("The commitment fact has no valid local transition.");
        }
        Revision = fact.Revision; _accepted.Add(fact.CommandId, (fact.IntentSha256, fact.ActorId));
    }
    public static bool ValidTerms(string kind, CommitmentTerms? value) => value is not null && Text(value.Statement, 4000) &&
        value.ServiceId != Uuid.Empty && value.OwnerPersonId != Uuid.Empty && value.BoundaryId != Uuid.Empty &&
        value.Categories is { Count: > 0 and <= 5 } && value.Categories.All(category => Categories.Contains(category)) &&
        value.Categories.Distinct(StringComparer.Ordinal).Count() == value.Categories.Count &&
        value.Citation is { } citation && CitationKinds.Contains(citation.Kind) && Text(citation.Title, 250) &&
        Text(citation.VersionOrDate, 100) && Text(citation.SectionLocator, 250) && citation.ArtifactId != Uuid.Empty &&
        (kind == "subservice_responsibility" ? value.ProviderId is { } provider && provider != Uuid.Empty : value.ProviderId is null);
    static CommitmentTerms Freeze(CommitmentTerms value) => value with { Categories = Array.AsReadOnly(value.Categories.ToArray()) };
    static bool Actor(ActorReference actor) => actor is { Kind: "member" } && Uuid.TryParse(actor.Id, out var id) && id != Uuid.Empty;
    static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;
    static CommandFailure Invalid() => CommandFailure.InvalidContent("The commitment content or transition is invalid.");
    static readonly HashSet<string> Kinds = ["service_commitment", "system_requirement", "user_entity_responsibility", "subservice_responsibility"];
    static readonly HashSet<string> Categories = ["security", "availability", "processing_integrity", "confidentiality", "privacy"];
    static readonly HashSet<string> CitationKinds = ["msa", "sla", "security_addendum", "privacy_notice", "policy", "other_contract"];
}
