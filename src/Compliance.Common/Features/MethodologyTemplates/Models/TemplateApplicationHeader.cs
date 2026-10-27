using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Current application admission and ordering authority, without downstream receipt or successor histories.</summary>
public sealed record TemplateApplicationHeader(Uuid TenantId, Uuid ProgramId, Uuid TemplateApplicationId,
    long Revision, Uuid TemplateId, Uuid TemplateVersionId, long TemplateVersionNumber, string TemplateContentSha256,
    string ApplicationKey, string Status, int DraftCount, int CreatedDraftCount, Uuid RequestedByMemberId,
    Uuid LatestSuccessorVersionId, long LatestSuccessorVersionNumber, bool HasPendingSuccessor,
    string ContentSha256, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset UpdatedAt);
