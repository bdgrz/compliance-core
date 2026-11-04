using Bdgrz.Compliance.Features.Controls.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Complete firm-authored control input; the destination owns validation and identifier admission.</summary>
public sealed record MethodologyTemplateControlContent(string Identifier, ControlDraftContent Content);
