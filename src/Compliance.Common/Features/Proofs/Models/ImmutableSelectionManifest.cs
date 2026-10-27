namespace Bdgrz.Compliance.Features.Proofs;

/// <summary>Reference to a complete stored selection; count and digest cover every item.</summary>
public sealed record ImmutableSelectionManifest(ImmutableContentRef Content, string ItemSchema, long Count,
    SelectionOrder Order, string ItemsSha256);
