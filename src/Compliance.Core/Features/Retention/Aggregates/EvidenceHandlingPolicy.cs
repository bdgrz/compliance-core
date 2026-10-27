using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Aggregates;

/// <summary>
/// One artifact policy stream retains the reviewed classification applied to the
/// immutable upload identity. Inspection, retention/holds, and redaction remain
/// with EvidenceArtifact, ArtifactRetention, and EvidenceRedaction respectively.
/// </summary>
public sealed class EvidenceHandlingPolicy : Aggregate
{
    public const long CurrentPolicyVersion = 1;
    const int MaximumRationaleLength = 2000;
    const int MaximumHistory = 32;
    static readonly HashSet<string> HandlingClasses = new(StringComparer.Ordinal)
    {
        "public", "internal", "confidential", "restricted"
    };
    static readonly HashSet<string> AssignableSourceStates = new(StringComparer.Ordinal)
    {
        "pending_inspection", "available", "quarantined"
    };

    readonly Uuid _tenantId;
    readonly Uuid _artifactId;
    EvidenceHandlingPolicyView? _current;

    public long Revision => _current?.Revision ?? 0;

    public EvidenceHandlingPolicyView? Current => _current;

    public EvidenceHandlingPolicy(Uuid tenantId, Uuid artifactId)
        : base(IdFor(tenantId, artifactId), new EventStreamAddress(tenantId.ToString(),
            "evidence-handling-policies", IdFor(tenantId, artifactId).ToString()))
    {
        _tenantId = tenantId;
        _artifactId = artifactId;
        On<ArtifactHandlingPolicyAssigned>(Apply);
        On<ArtifactHandlingPolicyRevised>(Apply);
    }

    public static Uuid IdFor(Uuid tenantId, Uuid artifactId)
    {
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty)
            throw new ArgumentException("An evidence handling policy requires tenant and artifact identities.");
        return Uuid.CreateVersion5(tenantId, $"evidence-handling-policy:{artifactId}");
    }

    public Result Assign(EvidenceHandlingPolicySourceSnapshot source, long expectedRevision,
        string handlingClass, long policyVersion, string rationale, ActorReference assignedBy,
        DateTimeOffset assignedAt)
    {
        if (ValidateSource(source) is { } sourceError)
            return Result.Failure(sourceError);
        if (ValidateActor(assignedBy) is { } actorError || assignedAt == default ||
            policyVersion != CurrentPolicyVersion || !ValidClass(handlingClass) ||
            string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > MaximumRationaleLength)
            return Invalid("Handling policy assignment needs the current policy, a supported class, rationale, and actor.");
        rationale = rationale.Trim();
        if (_current is { } current)
        {
            if (!SameSourceIdentity(current, source))
                return Conflict("The artifact content identity changed after its handling policy was assigned.");
            if (current.HandlingClass == handlingClass && current.PolicyVersion == policyVersion)
                return current.Rationale == rationale && current.AssignedBy == assignedBy &&
                       current.AssignedAt == assignedAt && expectedRevision <= Revision
                    ? Result.Success
                    : Conflict("The existing handling classification cannot be reassigned with changed intent.");
            if (expectedRevision != Revision)
                return StaleRevision();
            if (current.History.Count >= MaximumHistory)
                return Conflict("The artifact handling policy reached its retained version limit.");
        }
        else if (expectedRevision != 0)
            return StaleRevision();

        var revision = Revision + 1;
        var normalizedSource = FreezeSource(source);
        var historyEntry = new EvidenceHandlingPolicyHistoryEntry(revision, handlingClass,
            policyVersion, rationale, assignedBy, assignedAt);
        var state = new EvidenceHandlingPolicyView(_tenantId, _artifactId,
            normalizedSource.Source, normalizedSource.Position, normalizedSource.ContentLength,
            normalizedSource.SourceState, normalizedSource.SourceHandlingClass, handlingClass,
            policyVersion, revision, rationale, assignedBy, assignedAt,
            Array.AsReadOnly((_current?.History ?? []).Append(historyEntry).ToArray()));
        DomainEvent ev = _current is null
            ? new ArtifactHandlingPolicyAssigned(_tenantId, _artifactId, revision,
                MemberId(assignedBy), state, assignedAt)
            : new ArtifactHandlingPolicyRevised(_tenantId, _artifactId, revision,
                MemberId(assignedBy), state, assignedAt);
        RaiseEvent(ev);
        return Result.Success;
    }

    void Apply(ArtifactHandlingPolicyAssigned ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, creating: true);

    void Apply(ArtifactHandlingPolicyRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void ApplyState(Uuid tenantId, Uuid artifactId, long revision, Uuid? actorMemberId,
        EvidenceHandlingPolicyView state, DateTimeOffset occurredAt, bool creating = false)
    {
        if (tenantId != _tenantId || artifactId != _artifactId || state is null ||
            state.TenantId != _tenantId || state.ArtifactId != _artifactId ||
            state.Revision != revision || state.AssignedAt != occurredAt ||
            actorMemberId != MemberId(state.AssignedBy) || state.History is null ||
            state.History.Count != revision || revision != (creating ? 1 : Revision + 1) ||
            creating != (_current is null) || ValidateState(state) is not null ||
            _current is { } current && (!SameSourceIdentity(current, Source(state)) ||
                state.SourcePosition < current.SourcePosition ||
                state.History.Count != current.History.Count + 1 ||
                !state.History.Take(current.History.Count).SequenceEqual(current.History)))
            throw new InvalidOperationException("The evidence handling policy event is invalid.");
        _current = FreezeView(state);
    }

    RequestError? ValidateSource(EvidenceHandlingPolicySourceSnapshot source)
    {
        if (source is null || source.Source is null || source.Source.TenantId != _tenantId ||
            source.Source.SourceKind != "evidence_artifact" || source.Source.SourceId != _artifactId)
            return new(RequestErrorKind.NotFound, "The evidence artifact was not found in this tenant.");
        if (!ValidDigest(source.Source.ContentSha256) || source.Position == 0 ||
            source.ContentLength <= 0 || !ValidClass(source.SourceHandlingClass) ||
            !AssignableSourceStates.Contains(source.SourceState))
            return new(RequestErrorKind.Conflict,
                "Only an identified, non-rejected evidence artifact can receive a handling policy.");
        if (_current is { } current && source.Position < current.SourcePosition)
            return new(RequestErrorKind.Conflict, "The evidence source position regressed.", isTransient: true);
        return null;
    }

    static RequestError? ValidateActor(ActorReference actor) =>
        actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var memberId) && memberId != Uuid.Empty
            ? null
            : new(RequestErrorKind.Validation, "Handling policy decisions require a named personal member.");

    static RequestError? ValidateState(EvidenceHandlingPolicyView state)
    {
        if (state.Source is null || state.Source.SourceKind != "evidence_artifact" ||
            state.Source.TenantId != state.TenantId || state.Source.SourceId != state.ArtifactId ||
            !ValidDigest(state.Source.ContentSha256) || state.SourcePosition == 0 ||
            state.ContentLength <= 0 || !AssignableSourceStates.Contains(state.SourceState) ||
            !ValidClass(state.SourceHandlingClass) || !ValidClass(state.HandlingClass) ||
            state.PolicyVersion != CurrentPolicyVersion || state.Revision <= 0 ||
            state.History is null || state.History.Count != state.Revision ||
            state.History.Count > MaximumHistory ||
            !ValidActor(state.AssignedBy) || !ValidRationale(state.Rationale) || state.AssignedAt == default)
            return new(RequestErrorKind.Validation, "The retained evidence handling policy is invalid.");
        for (var index = 0; index < state.History.Count; index++)
        {
            var item = state.History[index];
            if (item.Revision != index + 1 || !ValidClass(item.HandlingClass) ||
                item.PolicyVersion != CurrentPolicyVersion || !ValidRationale(item.Rationale) ||
                !ValidActor(item.AssignedBy) || item.AssignedAt == default)
                return new(RequestErrorKind.Validation, "The handling policy history is invalid.");
        }
        var latest = state.History[^1];
        return latest.HandlingClass == state.HandlingClass && latest.PolicyVersion == state.PolicyVersion &&
               latest.Rationale == state.Rationale && latest.AssignedBy == state.AssignedBy &&
               latest.AssignedAt == state.AssignedAt
            ? null
            : new(RequestErrorKind.Validation, "The handling policy does not match its latest decision.");
    }

    static bool SameSourceIdentity(EvidenceHandlingPolicyView current,
        EvidenceHandlingPolicySourceSnapshot source) => current.Source == source.Source &&
        current.ContentLength == source.ContentLength && current.SourceHandlingClass == source.SourceHandlingClass;

    static bool SameSourceIdentity(EvidenceHandlingPolicyView first, EvidenceHandlingPolicyView second) =>
        first.Source == second.Source && first.ContentLength == second.ContentLength &&
        first.SourceHandlingClass == second.SourceHandlingClass;

    static EvidenceHandlingPolicySourceSnapshot Source(EvidenceHandlingPolicyView state) =>
        new(state.Source, state.SourcePosition, state.ContentLength, state.SourceState,
            state.SourceHandlingClass);

    static EvidenceHandlingPolicySourceSnapshot FreezeSource(EvidenceHandlingPolicySourceSnapshot source) =>
        source with { Source = source.Source with { } };

    static EvidenceHandlingPolicyView FreezeView(EvidenceHandlingPolicyView state) => state with
    {
        Source = state.Source with { },
        History = Array.AsReadOnly(state.History.ToArray()),
    };

    static Uuid? MemberId(ActorReference actor) => Uuid.TryParse(actor.Id, out var id) ? id : null;

    static bool ValidActor(ActorReference actor) => ValidateActor(actor) is null;

    static bool ValidClass(string? value) => value is not null && HandlingClasses.Contains(value);

    static bool ValidRationale(string? value) => value is { Length: > 0 and <= MaximumRationaleLength } &&
        value == value.Trim();

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result Invalid(string reason) => Result.Failure(new RequestError(RequestErrorKind.Validation, reason));

    static Result Conflict(string reason) => Result.Failure(new RequestError(RequestErrorKind.Conflict, reason));

    Result StaleRevision() => Conflict($"The handling policy revision is {Revision}.");
}
