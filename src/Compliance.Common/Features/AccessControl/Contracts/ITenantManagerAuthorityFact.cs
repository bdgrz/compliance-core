using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Current authority of the single episode affected by a native guard fact.</summary>
public interface ITenantManagerAuthorityFact : INativeOperationFact
{
    TenantManagerEpisodeAuthority CurrentAuthority { get; }
}
