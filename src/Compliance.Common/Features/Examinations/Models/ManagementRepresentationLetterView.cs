using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Externally sourced management representation letter and its retained signatures.</summary>
public sealed record ManagementRepresentationLetterView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid LetterId,
    ManagementRepresentationLetterContent Content,
    string Status,
    long Revision,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt,
    IReadOnlyList<ManagementRepresentationLetterSignatureView> Signatures,
    DateTimeOffset UpdatedAt);
