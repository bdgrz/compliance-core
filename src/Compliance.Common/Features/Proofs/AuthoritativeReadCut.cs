namespace Bdgrz.Compliance.Features.Proofs;

/// <summary>Captured exclusive global frontier; a complete empty cut has watermark zero.</summary>
public sealed record AuthoritativeReadCut(string AnchorSelector, ulong GlobalWatermarkExclusive,
    DateTimeOffset CapturedAt);
