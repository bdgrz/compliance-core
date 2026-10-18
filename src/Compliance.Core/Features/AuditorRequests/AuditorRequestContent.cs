using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The request terms captured from the auditor or assigned Attest collaborator.</summary>
public sealed record AuditorRequestContent(
    Uuid ExaminationId,
    string Source,
    string ExternalReference,
    string Wording,
    string Scope,
    string Priority,
    Uuid OwnerMemberId,
    DateOnly DueOn,
    bool RequiresInternalReview);
