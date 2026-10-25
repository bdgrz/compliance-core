using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Current authority of the single episode affected by a native guard fact.</summary>
public interface ITenantManagerAuthorityFact : INativeOperationFact
{
    TenantManagerEpisodeAuthority CurrentAuthority { get; }
}
