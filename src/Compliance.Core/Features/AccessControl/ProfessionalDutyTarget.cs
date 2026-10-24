using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The single frozen target of a professional-duty process record. The process factory is synchronous and sees only
///     the record, so everything a receiver command needs beyond the root source fact is carried in this one string:
///     the change kind (which lets a revocation skip the staff stages), the assertion, the designation, the signed
///     digest and, for recording, the pinned staff content.
/// </summary>
sealed record ProfessionalDutyTarget(ProfessionalDutyChangeKind Kind, Uuid AssertionId, Uuid DesignationId,
    string AssertionSha256, Uuid StaffMemberId, Uuid ContentEventId, ulong ContentRevision)
{
    const char Separator = '|';

    public bool IsRecording => Kind == ProfessionalDutyChangeKind.Record;

    /// <summary>The target an accepted change freezes, derived from its intent and verified assertion.</summary>
    public static ProfessionalDutyTarget For(ProfessionalDutyChangeIntent intent,
        VerifiedFirmProfessionalDutyAssertion evidence) => intent.StaffEligibility is { } requirement
            ? new ProfessionalDutyTarget(intent.ChangeKind, intent.VerifiedAssertionId, intent.DesignationId,
                evidence.EvidenceDigest,
                Uuid.Parse(requirement.SourceOwner.Resource, CultureInfo.InvariantCulture),
                requirement.ContentEvent.EventId, requirement.ResourceRevision)
            : new ProfessionalDutyTarget(intent.ChangeKind, intent.VerifiedAssertionId, intent.DesignationId,
                evidence.EvidenceDigest, Uuid.Empty, Uuid.Empty, 0);

    public static ProfessionalDutyTarget Parse(string value)
    {
        var parts = value.Split(Separator);
        if (parts.Length != 7 || !Enum.TryParse<ProfessionalDutyChangeKind>(parts[0], true, out var kind))
            throw new FormatException("A professional-duty target has seven parts.");
        return new ProfessionalDutyTarget(kind, Uuid.Parse(parts[1], CultureInfo.InvariantCulture),
            Uuid.Parse(parts[2], CultureInfo.InvariantCulture), parts[3],
            Uuid.Parse(parts[4], CultureInfo.InvariantCulture), Uuid.Parse(parts[5], CultureInfo.InvariantCulture),
            ulong.Parse(parts[6], CultureInfo.InvariantCulture));
    }

    public override string ToString() => string.Join(Separator, Kind.ToString().ToLowerInvariant(), AssertionId,
        DesignationId, AssertionSha256, StaffMemberId, ContentEventId, ContentRevision.ToString(CultureInfo.InvariantCulture));
}
