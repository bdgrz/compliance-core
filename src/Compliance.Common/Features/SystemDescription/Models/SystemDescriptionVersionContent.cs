using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>All immutable inputs that define the content identity of one narrative version.</summary>
public sealed record SystemDescriptionVersionContent(SystemDescriptionScope Scope,
    SystemDescriptionDraftContent Content);
