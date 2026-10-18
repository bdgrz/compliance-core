namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Exact approved successor snapshot proposed to one tenant application.</summary>
public sealed record TemplateApplicationSuccessorProposalView(
    MethodologyTemplateVersionView Version,
    string ContentSha256,
    string ProposedByDisplay,
    Cntryl.Portia.Uuid ProposedByMemberId,
    DateTimeOffset ProposedAt);
