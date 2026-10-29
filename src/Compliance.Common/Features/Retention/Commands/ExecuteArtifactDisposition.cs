using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the execute artifact disposition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.execute-artifact-disposition", 1)]
public sealed record ExecuteArtifactDisposition(Uuid TenantId, Uuid DispositionId, Uuid ArtifactId) : IRequest, IClientManagementMutationRequest;
