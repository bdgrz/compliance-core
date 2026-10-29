using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Proofs.Models;

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
