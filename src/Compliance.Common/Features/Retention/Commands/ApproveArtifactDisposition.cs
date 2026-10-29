using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the approve artifact disposition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.approve-artifact-disposition", 1)]
public sealed record ApproveArtifactDisposition(Uuid TenantId, Uuid DispositionId, Uuid ArtifactId, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;
