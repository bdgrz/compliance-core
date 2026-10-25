namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The exact authored publication and immutable version it produced.</summary>
public sealed record RiskMethodPublicationContent(PublishRiskMethodVersion Request, RiskMethodVersionView Version);
