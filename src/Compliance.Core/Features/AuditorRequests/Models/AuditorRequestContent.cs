using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

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
