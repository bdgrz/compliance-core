using System.Collections.ObjectModel;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Owns external letter provenance and the exact signed artifacts from required executives.</summary>
public sealed class ManagementRepresentationLetter : Aggregate
{
    const int MaximumSigners = 8;
    const int MaximumEventBytes = 64 * 1024;
    static readonly HashSet<string> ExecutiveRoles = new(StringComparer.OrdinalIgnoreCase)
        { "ceo", "ciso" };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    ManagementRepresentationLetterView? _current;

    public long Revision => _current?.Revision ?? 0;
    public ManagementRepresentationLetterView? Current => _current;

    public ManagementRepresentationLetter(Uuid tenantId, Uuid programId, Uuid letterId)
        : base(letterId, new EventStreamAddress(tenantId.ToString(), "management-representation-letters",
            letterId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<RepresentationLetterRecorded>(Apply);
        On<ManagementRepresentationLetterSigned>(Apply);
    }

    public CommandFailure? Record(ManagementRepresentationLetterContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId, actorDisplay, recordedAt)
                ? null
                : CommandFailure.StateConflict("The representation letter identity already has different content.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            !ValidActor(actorMemberId, actorDisplay, recordedAt))
            return Invalid("A representation letter requires stable identities and attributed management recording.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("The recorded representation letter must retain external provenance, an exact artifact, and named CEO/CISO signers.");

        var state = new ManagementRepresentationLetterView(_tenantId, _programId, Id,
            normalized, "awaiting_signatures", 1, actorMemberId, actorDisplay.Trim(),
            recordedAt, [], recordedAt);
        return Emit(state, actorMemberId, recordedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new RepresentationLetterRecorded(tenant, subject, revision, actor, snapshot, at),
            creating: true);
    }

    public CommandFailure? Sign(long expectedRevision, Uuid signerMemberId,
        Uuid signedArtifactId, string signedArtifactSha256, DateTimeOffset signedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The representation letter was not found.");
        if (signerMemberId == Uuid.Empty || signedAt == default || signedArtifactId == Uuid.Empty ||
            !Sha256(signedArtifactSha256))
            return Invalid("A signature requires an identified member, exact signed artifact, digest, and time.");
        var signer = current.Content.RequiredSigners.FirstOrDefault(item => item.MemberId == signerMemberId);
        if (signer is null || !ExecutiveRoles.Contains(signer.Role))
            return CommandFailure.StateConflict("Only a named CEO/CISO signer can sign this letter.");
        var prior = current.Signatures.FirstOrDefault(item => item.SignerMemberId == signerMemberId);
        if (prior is not null)
            return prior.SignedArtifactId == signedArtifactId &&
                   prior.SignedArtifactSha256 == NormalizeHash(signedArtifactSha256) && prior.SignedAt == signedAt
                ? null
                : CommandFailure.StateConflict("A signer's retained letter artifact is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "signed")
            return CommandFailure.StateConflict("A fully signed representation letter is immutable.");
        var signature = new ManagementRepresentationLetterSignatureView(signerMemberId,
            signer.Role, signedArtifactId, NormalizeHash(signedArtifactSha256), signedAt);
        var signatures = Append(current.Signatures, signature);
        var state = current with
        {
            Status = signatures.Count == current.Content.RequiredSigners.Count
                ? "signed" : "awaiting_signatures",
            Revision = Revision + 1,
            Signatures = signatures,
            UpdatedAt = signedAt,
        };
        return Emit(state, signerMemberId, signedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ManagementRepresentationLetterSigned(tenant, subject, revision, actor, snapshot, at));
    }

    void Apply(RepresentationLetterRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, creating: true);

    void Apply(ManagementRepresentationLetterSigned ev) => ApplyState(ev.TenantId,
        ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        ManagementRepresentationLetterView state, DateTimeOffset occurredAt, bool creating = false)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.LetterId != Id ||
            state.Revision != revision || occurredAt == default || revision != Revision + 1 ||
            creating != (_current is null) || actorMemberId == Uuid.Empty || !ValidState(state) ||
            _current is null && state.RecordedByMemberId != actorMemberId ||
            _current is { } prior && (!ValidTransition(prior, state) ||
                state.Signatures[^1].SignerMemberId != actorMemberId))
            throw new InvalidOperationException("The representation letter event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(ManagementRepresentationLetterView state, Uuid? actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, ManagementRepresentationLetterView, DateTimeOffset, DomainEvent> createEvent,
        bool creating = false)
    {
        if (!ValidState(state) ||
            (creating ? _current is not null : _current is null || !ValidTransition(_current, state)))
            return Invalid("The representation letter transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return Invalid("The complete representation letter exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    bool ValidState(ManagementRepresentationLetterView state)
    {
        var content = NormalizeContent(state.Content);
        if (content is null || state.TenantId != _tenantId || state.ProgramId != _programId ||
            state.LetterId != Id || state.Revision <= 0 || state.RecordedByMemberId == Uuid.Empty ||
            !Bounded(state.RecordedByDisplay, 1, 200) || state.RecordedAt == default ||
            state.UpdatedAt == default || state.Signatures is null ||
            state.Signatures.Count > content.RequiredSigners.Count)
            return false;
        foreach (var signature in state.Signatures)
        {
            var required = content.RequiredSigners.FirstOrDefault(item => item.MemberId == signature.SignerMemberId);
            if (required is null || required.Role != signature.SignerRole ||
                signature.SignedArtifactId == Uuid.Empty || !Sha256(signature.SignedArtifactSha256) ||
                signature.SignedAt == default)
                return false;
        }
        if (state.Signatures.Select(item => item.SignerMemberId).Distinct().Count() != state.Signatures.Count ||
            state.Signatures.Select(item => item.SignedArtifactId).Distinct().Count() != state.Signatures.Count)
            return false;
        return state.Status == (state.Signatures.Count == content.RequiredSigners.Count
            ? "signed" : "awaiting_signatures");
    }

    static bool ValidTransition(ManagementRepresentationLetterView prior,
        ManagementRepresentationLetterView next) =>
        next.Revision == prior.Revision + 1 && next.TenantId == prior.TenantId &&
        next.ProgramId == prior.ProgramId && next.LetterId == prior.LetterId &&
        Digest(next.Content) == Digest(prior.Content) && next.RecordedByMemberId == prior.RecordedByMemberId &&
        next.RecordedAt == prior.RecordedAt && next.Signatures.Count == prior.Signatures.Count + 1 &&
        prior.Signatures.SequenceEqual(next.Signatures.Take(prior.Signatures.Count));

    static ManagementRepresentationLetterContent? NormalizeContent(
        ManagementRepresentationLetterContent? content)
    {
        if (content is null || content.ExaminationId == Uuid.Empty ||
            content.ScopeKind is not ("type_i" or "type_ii") || content.PackageId == Uuid.Empty ||
            content.PackageVersion <= 0 || content.PackageManifestId == Uuid.Empty ||
            !Sha256(content.PackageManifestSha256) || content.AssertionId == Uuid.Empty ||
            content.AssertionVersion <= 0 || !Sha256(content.AssertionContentSha256) ||
            content.SourceKind != "auditor" ||
            !Bounded(content.SourceReference, 1, 500) || content.OriginalArtifactId == Uuid.Empty ||
            !Sha256(content.OriginalArtifactSha256) || content.LetterDate == default ||
            content.RequiredSigners is null || content.RequiredSigners.Count is < 1 or > MaximumSigners ||
            content.RequiredSigners.Any(item => item is null || item.MemberId == Uuid.Empty ||
                !ExecutiveRoles.Contains(item.Role)) ||
            content.RequiredSigners.Select(item => item.MemberId).Distinct().Count() != content.RequiredSigners.Count)
            return null;
        return content with
        {
            ScopeKind = content.ScopeKind.Trim(),
            PackageManifestSha256 = NormalizeHash(content.PackageManifestSha256),
            AssertionContentSha256 = NormalizeHash(content.AssertionContentSha256),
            SourceReference = content.SourceReference.Trim(),
            OriginalArtifactSha256 = NormalizeHash(content.OriginalArtifactSha256),
            RequiredSigners = Freeze(content.RequiredSigners.Select(item => item with
            { Role = item.Role.Trim() }).OrderBy(item => item.MemberId.ToString(), StringComparer.Ordinal).ToArray()),
        };
    }

    static bool SameInitial(ManagementRepresentationLetterView state,
        ManagementRepresentationLetterContent? content, Uuid actor, string display, DateTimeOffset at) =>
        NormalizeContent(content) is { } normalized && Digest(normalized) == Digest(state.Content) &&
        state.RecordedByMemberId == actor && state.RecordedByDisplay == Clean(display) && state.RecordedAt == at;

    static bool ValidActor(Uuid actor, string display, DateTimeOffset at) => actor != Uuid.Empty &&
        Bounded(display, 1, 200) && at != default;
    static string Digest<T>(T value) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)))
        .ToLowerInvariant();
    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("management representation letter", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static ManagementRepresentationLetterView Freeze(ManagementRepresentationLetterView state) => state with
    {
        Content = NormalizeContent(state.Content)!,
        Signatures = Freeze(state.Signatures.ToArray()),
    };
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) =>
        Freeze(values.Append(value).ToArray());
}
