using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Aggregates;

/// <summary>
/// Owns one immutable management sign-off over exact package, assertion, letter, outcome,
/// exception, and unresolved-item identities. Those source records retain their own lifecycles.
/// </summary>
public sealed class ExaminationSignOff : Aggregate
{
    const int MaximumItems = 1000;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    const int MaximumTextLength = 4000;
    static readonly HashSet<string> ExecutiveRoles = new(StringComparer.OrdinalIgnoreCase)
        { "ceo", "ciso" };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    ExaminationSignOffView? _current;

    public long Revision => _current?.Revision ?? 0;
    public ExaminationSignOffView? Current => _current;

    public ExaminationSignOff(Uuid tenantId, Uuid programId, Uuid signOffId)
        : base(signOffId, new EventStreamAddress(tenantId.ToString(), "examination-signoffs",
            signOffId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ExaminationSignedOff>(Apply);
    }

    public CommandFailure? SignOff(ExaminationSignOffContent content, Uuid actorMemberId,
        string approverRole, string approverDisplay, DateTimeOffset signedOffAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId, approverRole, approverDisplay, signedOffAt)
                ? null
                : CommandFailure.StateConflict("An examination sign-off is immutable once recorded.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            actorMemberId == Uuid.Empty || signedOffAt == default || !Bounded(approverDisplay, 1, 200))
            return Invalid("A management sign-off requires stable identities and attributed approval.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("The management sign-off must bind exact examination records and acknowledge every unresolved material item.");
        var signer = normalized.Approvers.FirstOrDefault(item => item.MemberId == actorMemberId);
        if (signer is null || !ExecutiveRoles.Contains(signer.Role) ||
            !StringComparer.OrdinalIgnoreCase.Equals(signer.Role, approverRole))
            return CommandFailure.ActorProhibited("Only an identified CEO/CISO approver named in the sign-off can authorize it.");

        var state = new ExaminationSignOffView(_tenantId, _programId, Id, normalized,
            normalized.Decision, 1, actorMemberId, signer.Role, approverDisplay.Trim(),
            signedOffAt, signedOffAt);
        var ev = new ExaminationSignedOff(_tenantId, Id, 1, actorMemberId, state, signedOffAt);
        if (DomainEventPayload.Size(ev) > MaximumEventBytes)
            return Invalid("The complete examination sign-off exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    void Apply(ExaminationSignedOff ev)
    {
        if (ev.TenantId != _tenantId || ev.SubjectId != Id || ev.State is null ||
            ev.State.TenantId != _tenantId || ev.State.ProgramId != _programId ||
            ev.State.ExaminationSignOffId != Id || ev.Revision != 1 || ev.Revision != Revision + 1 ||
            ev.State.Revision != ev.Revision || ev.State.Status != ev.State.Content.Decision ||
            ev.ActorMemberId is not { } actor || actor == Uuid.Empty ||
            ev.State.SignedOffByMemberId != actor || ev.State.SignedOffAt != ev.OccurredAt ||
            ev.State.UpdatedAt != ev.OccurredAt || ev.OccurredAt == default ||
            NormalizeContent(ev.State.Content) is not { } normalized ||
            Digest(normalized) != Digest(ev.State.Content) ||
            !normalized.Approvers.Any(item => item.MemberId == actor &&
                StringComparer.OrdinalIgnoreCase.Equals(item.Role, ev.State.ApproverRole)) ||
            !ExecutiveRoles.Contains(ev.State.ApproverRole))
            throw new InvalidOperationException("The examination sign-off event is invalid.");
        _current = ev.State with { Content = normalized };
    }

    static ExaminationSignOffContent? NormalizeContent(ExaminationSignOffContent? content)
    {
        if (content is null || content.ExaminationId == Uuid.Empty ||
            content.ScopeKind is not ("type_i" or "type_ii") || content.SourceSnapshotId == Uuid.Empty ||
            !Sha256(content.SourceSnapshotSha256) || content.PackageId == Uuid.Empty ||
            content.PackageVersion <= 0 || content.PackageManifestId == Uuid.Empty ||
            !Sha256(content.PackageManifestSha256) || content.AssertionId == Uuid.Empty ||
            content.AssertionVersion <= 0 || !Sha256(content.AssertionContentSha256) ||
            content.RepresentationLetterId == Uuid.Empty ||
            !Sha256(content.RepresentationLetterContentSha256) || content.AuditOutcomeId == Uuid.Empty ||
            !Sha256(content.AuditOutcomeContentSha256) || content.KnownExceptions is null ||
            content.UnresolvedMaterialItemIds is null || content.AcknowledgedMaterialItemIds is null ||
            content.UnresolvedMaterialItemIds.Count > MaximumItems ||
            content.AcknowledgedMaterialItemIds.Count > MaximumItems ||
            content.Decision is not ("approved" or "rejected") ||
            !Bounded(content.Comments, 1, MaximumTextLength) || content.Approvers is null ||
            content.Approvers.Count is < 1 or > 8 || content.Commitments is null ||
            content.Commitments.Count > MaximumItems)
            return null;
        if (content.KnownExceptions.Any(item => item is null || !Token(item.SourceType, 80) ||
                item.RecordId == Uuid.Empty || item.VersionId == Uuid.Empty || item.Version <= 0 ||
                !Sha256(item.ContentSha256)) ||
            content.KnownExceptions.Select(item => (item.SourceType, item.RecordId, item.VersionId))
                .Distinct().Count() != content.KnownExceptions.Count ||
            content.UnresolvedMaterialItemIds.Any(item => item == Uuid.Empty) ||
            content.AcknowledgedMaterialItemIds.Any(item => item == Uuid.Empty) ||
            content.UnresolvedMaterialItemIds.Distinct().Count() != content.UnresolvedMaterialItemIds.Count ||
            content.AcknowledgedMaterialItemIds.Distinct().Count() != content.AcknowledgedMaterialItemIds.Count ||
            !content.UnresolvedMaterialItemIds.OrderBy(item => item.ToString(), StringComparer.Ordinal)
                .SequenceEqual(content.AcknowledgedMaterialItemIds.OrderBy(item => item.ToString(), StringComparer.Ordinal)) ||
            content.Approvers.Any(item => item is null || item.MemberId == Uuid.Empty ||
                !ExecutiveRoles.Contains(item.Role)) ||
            content.Approvers.Select(item => item.MemberId).Distinct().Count() != content.Approvers.Count ||
            content.Commitments.Any(item => item is null || item.CommitmentId == Uuid.Empty ||
                !Bounded(item.Description, 1, MaximumTextLength) || item.OwnerMemberId == Uuid.Empty ||
                item.DueOn == default || !Token(item.SourceType, 80) ||
                item.SourceRecordId == Uuid.Empty || item.SourceRecordVersion <= 0 ||
                !Sha256(item.SourceContentSha256)) ||
            content.Commitments.Select(item => item.CommitmentId).Distinct().Count() != content.Commitments.Count)
            return null;
        return content with
        {
            ScopeKind = content.ScopeKind.Trim(),
            SourceSnapshotSha256 = NormalizeHash(content.SourceSnapshotSha256),
            PackageManifestSha256 = NormalizeHash(content.PackageManifestSha256),
            AssertionContentSha256 = NormalizeHash(content.AssertionContentSha256),
            RepresentationLetterContentSha256 = NormalizeHash(content.RepresentationLetterContentSha256),
            AuditOutcomeContentSha256 = NormalizeHash(content.AuditOutcomeContentSha256),
            Comments = content.Comments.Trim(),
            KnownExceptions = Array.AsReadOnly(content.KnownExceptions.Select(item => item with
            {
                SourceType = item.SourceType.Trim(),
                ContentSha256 = NormalizeHash(item.ContentSha256),
            }).OrderBy(item => item.SourceType, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ToArray()),
            UnresolvedMaterialItemIds = FreezeIds(content.UnresolvedMaterialItemIds),
            AcknowledgedMaterialItemIds = FreezeIds(content.AcknowledgedMaterialItemIds),
            Approvers = Array.AsReadOnly(content.Approvers.Select(item => item with
            { Role = item.Role.Trim() }).OrderBy(item => item.MemberId.ToString(), StringComparer.Ordinal).ToArray()),
            Commitments = Array.AsReadOnly(content.Commitments.Select(item => item with
            {
                Description = item.Description.Trim(),
                SourceType = item.SourceType.Trim().ToLowerInvariant(),
                SourceContentSha256 = NormalizeHash(item.SourceContentSha256),
            }).OrderBy(item => item.CommitmentId.ToString(), StringComparer.Ordinal).ToArray()),
        };
    }

    static bool SameInitial(ExaminationSignOffView state, ExaminationSignOffContent? content,
        Uuid actor, string role, string display, DateTimeOffset at) =>
        NormalizeContent(content) is { } normalized && Digest(normalized) == Digest(state.Content) &&
        state.SignedOffByMemberId == actor &&
        StringComparer.OrdinalIgnoreCase.Equals(state.ApproverRole, role.Trim()) &&
        state.ApproverDisplay == display.Trim() && state.SignedOffAt == at;

    static string Digest<T>(T content) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content))).ToLowerInvariant();
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool Token(string? value, int maximum) => Bounded(value, 1, maximum) &&
        value!.Trim().All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    static ReadOnlyCollection<Uuid> FreezeIds(IEnumerable<Uuid> ids) =>
        Array.AsReadOnly(ids.OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray());
}
