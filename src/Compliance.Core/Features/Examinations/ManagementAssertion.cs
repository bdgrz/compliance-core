using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
/// Owns immutable management assertion versions and independent CEO/CISO decisions.
/// Package, baseline, system description, and supporting records remain separate roots;
/// this stream binds their exact identities and hashes without copying their state.
/// </summary>
public sealed class ManagementAssertion : Aggregate
{
    const int MaximumVersions = 32;
    const int MaximumReferences = 256;
    const int MaximumEventBytes = 256 * 1024;
    const int MaximumTextLength = 4000;
    static readonly HashSet<string> ScopeKinds = new(StringComparer.Ordinal)
        { "type_i", "type_ii" };
    static readonly HashSet<string> WordingSources = new(StringComparer.Ordinal)
        { "auditor", "management", "organization_template" };
    static readonly HashSet<string> ExecutiveRoles = new(StringComparer.OrdinalIgnoreCase)
        { "ceo", "ciso" };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    ManagementAssertionView? _current;

    public long Revision => _current?.Revision ?? 0;
    public ManagementAssertionView? Current => _current;

    public ManagementAssertion(Uuid tenantId, Uuid programId, Uuid assertionId)
        : base(assertionId, new EventStreamAddress(tenantId.ToString(), "management-assertions",
            assertionId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ManagementAssertionRecorded>(Apply);
        On<ManagementAssertionRevised>(Apply);
        On<ManagementAssertionApproved>(Apply);
    }

    public CommandFailure? Create(ManagementAssertionContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset createdAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId, actorDisplay)
                ? null
                : CommandFailure.StateConflict("The management assertion already exists with different content.");
        if (!ValidIdentity() || ValidateActor(actorMemberId, actorDisplay, createdAt) is not null)
            return Invalid("An assertion requires tenant, program, record, and attributed actor identities.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("The assertion must bind exact examination inputs, wording, criteria, controls, exceptions, and signers.");

        var version = NewVersion(normalized, 1, actorMemberId, actorDisplay.Trim(), createdAt, "");
        var state = new ManagementAssertionView(_tenantId, _programId, Id, normalized.ExaminationId,
            normalized.ScopeKind, "draft", 1, 1, version, [version], [], createdAt);
        return Emit(state, actorMemberId, createdAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ManagementAssertionRecorded(tenant, subject, revision, actor, snapshot, at),
            creating: true);
    }

    public CommandFailure? Revise(long expectedRevision, ManagementAssertionContent content,
        string changeReason, Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The management assertion was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Versions.Count >= MaximumVersions || current.Status == "retired")
            return CommandFailure.StateConflict("The assertion is retired or has reached its version limit.");
        if (ValidateActor(actorMemberId, actorDisplay, revisedAt) is { } actorFailure)
            return actorFailure;
        changeReason = Clean(changeReason);
        if (!Bounded(changeReason, 1, MaximumTextLength) || NormalizeContent(content) is not { } normalized)
            return Invalid("A revised assertion requires complete exact inputs and a bounded change reason.");
        if (normalized.ExaminationId != current.ExaminationId || normalized.ScopeKind != current.ScopeKind)
            return CommandFailure.StateConflict("An assertion cannot change its examination or scope.");
        if (SameContent(current.CurrentVersion!.Content, normalized))
            return CommandFailure.StateConflict("A new assertion version must change its wording or bound inputs.");

        var version = NewVersion(normalized, current.CurrentVersionNumber + 1, actorMemberId,
            actorDisplay.Trim(), revisedAt, changeReason);
        var state = current with
        {
            Status = "draft",
            Revision = Revision + 1,
            CurrentVersionNumber = version.Version,
            CurrentVersion = version,
            Versions = Append(current.Versions, version),
            UpdatedAt = revisedAt,
        };
        return Emit(state, actorMemberId, revisedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ManagementAssertionRevised(tenant, subject, revision, actor, snapshot, at));
    }

    public CommandFailure? Approve(long expectedRevision, int assertionVersion, string outcome,
        string rationale, Uuid approverMemberId, string approverRole, string approverDisplay,
        DateTimeOffset approvedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The management assertion was not found.");
        if (ValidateActor(approverMemberId, approverDisplay, approvedAt) is { } actorFailure)
            return actorFailure;
        outcome = Clean(outcome).ToLowerInvariant();
        rationale = Clean(rationale);
        approverRole = Clean(approverRole).ToLowerInvariant();
        var version = current.Versions.FirstOrDefault(item => item.Version == assertionVersion);
        if (version is null)
            return CommandFailure.MissingRecord("The assertion version was not found.");
        var prior = current.Approvals.FirstOrDefault(item => item.AssertionVersion == assertionVersion);
        if (prior is not null)
            return prior.Outcome == outcome && prior.Rationale == rationale &&
                   prior.ApprovedByMemberId == approverMemberId &&
                   StringComparer.OrdinalIgnoreCase.Equals(prior.ApproverRole, approverRole)
                ? null
                : CommandFailure.StateConflict("An assertion version has one immutable management decision.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (assertionVersion != current.CurrentVersionNumber || current.Status != "draft")
            return CommandFailure.StateConflict("Only the current draft assertion version can be approved.");
        if (outcome is not ("approve" or "reject") || !Bounded(rationale, 1, MaximumTextLength) ||
            !ExecutiveRoles.Contains(approverRole) ||
            !version.Content.Signers.Any(signer => signer.MemberId == approverMemberId &&
                StringComparer.OrdinalIgnoreCase.Equals(signer.Role, approverRole)) ||
            approverMemberId == version.AuthoredByMemberId)
            return CommandFailure.ActorProhibited(
                "Assertion approval requires a named CEO/CISO signer independent of its author and a rationale.");

        var approval = new ManagementAssertionApprovalView(Uuid.CreateVersion4(), assertionVersion,
            outcome, rationale, version.AuthoredByMemberId, approverMemberId, approverRole,
            approverDisplay.Trim(), approvedAt);
        var state = current with
        {
            Status = outcome == "approve" ? "approved" : "rejected",
            Revision = Revision + 1,
            Approvals = Append(current.Approvals, approval),
            UpdatedAt = approvedAt,
        };
        return Emit(state, approverMemberId, approvedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ManagementAssertionApproved(tenant, subject, revision, actor, snapshot, at));
    }

    void Apply(ManagementAssertionRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, creating: true);

    void Apply(ManagementAssertionRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(ManagementAssertionApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        ManagementAssertionView state, DateTimeOffset occurredAt, bool creating = false)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId ||
            state.ManagementAssertionId != Id || state.Revision != revision ||
            occurredAt == default || revision != Revision + 1 ||
            creating != (_current is null) || ValidateState(state) is not null ||
            actorMemberId == Uuid.Empty || _current is null &&
                state.CurrentVersion?.AuthoredByMemberId != actorMemberId ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId))
            throw new InvalidOperationException("The management assertion event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(ManagementAssertionView state, Uuid? actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, ManagementAssertionView, DateTimeOffset, DomainEvent> createEvent,
        bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null || state.CurrentVersion?.AuthoredByMemberId != actorMemberId
                : _current is null || !ValidTransition(_current, state, actorMemberId)))
            return Invalid("The assertion transition is not a valid next state.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return Invalid("The complete assertion state exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(ManagementAssertionView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId ||
            state.ManagementAssertionId != Id || state.ExaminationId == Uuid.Empty ||
            !ScopeKinds.Contains(state.ScopeKind) || state.Revision <= 0 ||
            state.Versions is null || state.Versions.Count is < 1 or > MaximumVersions ||
            state.Approvals is null || state.CurrentVersion is null ||
            state.CurrentVersion.Version != state.CurrentVersionNumber ||
            state.CurrentVersionNumber != state.Versions.Count ||
            state.Versions.Where((version, index) => version.Version != index + 1 ||
                NormalizeContent(version.Content) is not { } normalized ||
                Digest(normalized) != Digest(version.Content) || !Sha256(version.ContentSha256) ||
                version.ContentSha256 != Digest(normalized) ||
                version.AuthoredByMemberId == Uuid.Empty ||
                !Bounded(version.AuthoredByDisplay, 1, 200) || version.AuthoredAt == default ||
                !Bounded(version.ChangeReason, 0, MaximumTextLength)).Any() ||
            state.Approvals.Count > state.Versions.Count ||
            state.Approvals.Select(item => item.AssertionVersion).Distinct().Count() != state.Approvals.Count ||
            state.Approvals.Any(item => item.ApprovalId == Uuid.Empty ||
                item.AssertionVersion < 1 || item.AssertionVersion > state.CurrentVersionNumber ||
                item.Outcome is not ("approve" or "reject") || !Bounded(item.Rationale, 1, MaximumTextLength) ||
                item.ApprovedByMemberId == Uuid.Empty || !ExecutiveRoles.Contains(item.ApproverRole) ||
                !Bounded(item.ApproverDisplay, 1, 200) || item.ApprovedAt == default))
            return Invalid("The assertion event contains an invalid state snapshot.");
        var expectedStatus = state.Approvals.LastOrDefault(item =>
            item.AssertionVersion == state.CurrentVersionNumber)?.Outcome switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            _ => "draft",
        };
        return state.Status == expectedStatus ? null : Invalid("The assertion status does not match its decision history.");
    }

    static bool ValidTransition(ManagementAssertionView prior, ManagementAssertionView next,
        Uuid? actorMemberId)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.ManagementAssertionId != prior.ManagementAssertionId ||
            next.ExaminationId != prior.ExaminationId || next.ScopeKind != prior.ScopeKind ||
            next.Versions.Count < prior.Versions.Count || next.Versions.Count > prior.Versions.Count + 1)
            return false;
        if (next.Versions.Count == prior.Versions.Count)
            return SameVersions(prior.Versions, next.Versions) &&
                   next.Approvals.Count == prior.Approvals.Count + 1 &&
                   next.Approvals.Take(prior.Approvals.Count).SequenceEqual(prior.Approvals) &&
                   next.Approvals[^1].ApprovedByMemberId == actorMemberId;
        return SameVersions(prior.Versions, next.Versions.Take(prior.Versions.Count).ToArray()) &&
               next.CurrentVersionNumber == prior.CurrentVersionNumber + 1 &&
               next.Approvals.SequenceEqual(prior.Approvals) &&
               next.CurrentVersion?.AuthoredByMemberId == actorMemberId &&
               next.Versions[^1].AuthoredAt == next.UpdatedAt;
    }

    static ManagementAssertionContent? NormalizeContent(ManagementAssertionContent? content)
    {
        if (content is null || content.ExaminationId == Uuid.Empty ||
            !ScopeKinds.Contains(content.ScopeKind) || content.SourceSnapshotId == Uuid.Empty ||
            !Sha256(content.SourceSnapshotSha256) || content.SystemDescriptionVersionId == Uuid.Empty ||
            !Sha256(content.SystemDescriptionSha256) || content.PackageId == Uuid.Empty ||
            content.PackageVersion <= 0 || content.PackageManifestId == Uuid.Empty ||
            !Sha256(content.PackageManifestSha256) || !WordingSources.Contains(content.WordingSource) ||
            content.WordingReferenceId == Uuid.Empty || !Bounded(content.AssertionText, 1, MaximumTextLength) ||
            content.SupportingInputs is null || content.SupportingInputs.Count is < 2 or > MaximumReferences ||
            content.Signers is null || content.Signers.Count is < 1 or > 8)
            return null;
        var refs = content.SupportingInputs;
        if (refs.Any(item => item is null || !Token(item.SourceType, 80) || item.RecordId == Uuid.Empty ||
                item.VersionId == Uuid.Empty || item.Version <= 0 || !Sha256(item.ContentSha256)) ||
            refs.Select(item => (item.SourceType, item.RecordId, item.VersionId)).Distinct().Count() != refs.Count ||
            !refs.Any(item => item.SourceType == "criteria") ||
            !refs.Any(item => item.SourceType == "control") ||
            content.Signers.Any(item => item is null || item.MemberId == Uuid.Empty ||
                !ExecutiveRoles.Contains(item.Role)) ||
            content.Signers.Select(item => item.MemberId).Distinct().Count() != content.Signers.Count)
            return null;
        return content with
        {
            ScopeKind = Clean(content.ScopeKind),
            WordingSource = Clean(content.WordingSource),
            AssertionText = Clean(content.AssertionText),
            SourceSnapshotSha256 = NormalizeHash(content.SourceSnapshotSha256),
            SystemDescriptionSha256 = NormalizeHash(content.SystemDescriptionSha256),
            PackageManifestSha256 = NormalizeHash(content.PackageManifestSha256),
            SupportingInputs = Freeze(refs.Select(item => item with
            {
                SourceType = Clean(item.SourceType),
                ContentSha256 = NormalizeHash(item.ContentSha256),
            }).OrderBy(item => item.SourceType, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ToArray()),
            Signers = Freeze(content.Signers.Select(item => item with { Role = Clean(item.Role) })
                .OrderBy(item => item.MemberId.ToString(), StringComparer.Ordinal).ToArray()),
        };
    }

    static ManagementAssertionVersionView NewVersion(ManagementAssertionContent content, int version,
        Uuid actor, string display, DateTimeOffset at, string reason)
    {
        var hash = Digest(content);
        return new ManagementAssertionVersionView(version, content, hash, "recorded", actor,
            display, at, reason);
    }

    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();

    static bool SameInitial(ManagementAssertionView state, ManagementAssertionContent? content,
        Uuid actorId, string actorDisplay) =>
        NormalizeContent(content) is { } normalized && state.CurrentVersion is { } version &&
        SameContent(version.Content, normalized) && version.AuthoredByMemberId == actorId &&
        version.AuthoredByDisplay == Clean(actorDisplay);

    static bool SameContent(ManagementAssertionContent left, ManagementAssertionContent right) =>
        Digest(left) == Digest(right);

    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;
    static CommandFailure? ValidateActor(Uuid? id, string? display, DateTimeOffset at)
    {
        if (id is not { } memberId || memberId == Uuid.Empty ||
            !Bounded(display, 1, 200) || at == default)
            return CommandFailure.InvalidContent("A human action requires a member, display name, and timestamp.");
        return null;
    }
    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("management assertion", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool Token(string? value, int maximum) => Bounded(value, 1, maximum) &&
        value!.Trim().All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(static character =>
        char.IsAsciiHexDigit(character));
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static ManagementAssertionView Freeze(ManagementAssertionView state)
    {
        var versions = Freeze(state.Versions.Select(version => version with
        { Content = NormalizeContent(version.Content)! }).ToArray());
        return state with
        {
            CurrentVersion = versions[^1],
            Versions = versions,
            Approvals = Freeze(state.Approvals.ToArray()),
        };
    }
    static bool SameVersions(IReadOnlyList<ManagementAssertionVersionView> left,
        IReadOnlyList<ManagementAssertionVersionView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => pair.First.Version == pair.Second.Version &&
            pair.First.ContentSha256 == pair.Second.ContentSha256 &&
            pair.First.Status == pair.Second.Status &&
            pair.First.AuthoredByMemberId == pair.Second.AuthoredByMemberId &&
            pair.First.AuthoredAt == pair.Second.AuthoredAt &&
            pair.First.ChangeReason == pair.Second.ChangeReason);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) =>
        Freeze(values.Append(value).ToArray());
}
