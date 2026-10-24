namespace Bdgrz.Compliance.Features.Proofs;

/// <summary>How the items of an immutable selection are ordered.</summary>
public enum SelectionOrder
{
    /// <summary>The authored input order is meaningful and preserved.</summary>
    Authored,

    /// <summary>Items are ordered by ordinal UTF-8 comparison of the declared key.</summary>
    CanonicalKey,
}
