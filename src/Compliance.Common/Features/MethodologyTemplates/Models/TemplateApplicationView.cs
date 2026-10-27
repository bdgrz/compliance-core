using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Tenant-owned application and provenance, isolated to one organization and program.</summary>
public sealed record TemplateApplicationView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid TemplateApplicationId,
    string Status,
    long Revision,
    Uuid TemplateId,
    Uuid TemplateVersionId,
    long TemplateVersionNumber,
    string TemplateContentSha256,
    string ApplicationKey,
    string ContentSha256,
    MethodologyTemplateVersionView SourceVersion,
    IReadOnlyList<TemplateApplicationDraftView> Drafts,
    IReadOnlyList<TemplateApplicationSuccessorProposalView> SuccessorProposals,
    IReadOnlyList<TemplateApplicationSuccessorDecisionView> SuccessorDecisions,
    bool IsActivated,
    Uuid RequestedByMemberId,
    string RequestedByDisplay,
    DateTimeOffset RequestedAt,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt);
