namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>All immutable inputs that define the content identity of one narrative version.</summary>
public sealed record SystemDescriptionVersionContent(SystemDescriptionScope Scope,
    SystemDescriptionDraftContent Content);
