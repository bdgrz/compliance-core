namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>One complete business change; later changes never repeat the application or its accumulated histories.</summary>
public sealed record TemplateApplicationMutationContent(string Transition, TemplateApplicationView? Application,
    TemplateApplicationDraftView? Draft, TemplateApplicationSuccessorProposalView? Proposal,
    TemplateApplicationSuccessorDecisionView? Decision);
