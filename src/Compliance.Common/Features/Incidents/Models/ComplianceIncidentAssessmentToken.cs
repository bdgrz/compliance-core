using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Historical assessment identity/attribution needed for exact delayed-reaction classification without narrative history.</summary>
public sealed record ComplianceIncidentAssessmentToken(int ContentVersion, string AssessmentSha256, Uuid AssessorMemberId,
    DateTimeOffset AssessedAt, bool RequiresSystemDescriptionReview);
