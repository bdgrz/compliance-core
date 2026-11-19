using Bdgrz.Compliance.Features.Policies.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Complete firm-authored policy input, including its actual audience and review cadence.</summary>
public sealed record MethodologyTemplatePolicyContent(string Identifier, PolicyContent Content);
