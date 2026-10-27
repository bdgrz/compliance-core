using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the assign artifact handling policy intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.assign-artifact-handling-policy", 1)]
public sealed record AssignArtifactHandlingPolicy(Uuid TenantId, Uuid ArtifactId, long ExpectedRevision, string HandlingClass, long PolicyVersion, string Rationale) : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
