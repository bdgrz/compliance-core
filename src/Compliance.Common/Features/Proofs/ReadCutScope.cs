namespace Bdgrz.Compliance.Features.Proofs;

/// <summary>Breadth of an authoritative read cut.</summary>
public enum ReadCutScope
{
    /// <summary>A single resource.</summary>
    Resource,

    /// <summary>A resource area.</summary>
    Area,

    /// <summary>The whole realm.</summary>
    Realm,
}
