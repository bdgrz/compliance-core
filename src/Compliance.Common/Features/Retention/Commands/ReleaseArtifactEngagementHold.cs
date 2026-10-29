using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the release artifact engagement hold intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.release-artifact-engagement-hold", 1)]
public sealed record ReleaseArtifactEngagementHold(Uuid TenantId, Uuid HoldId, long ExpectedRevision, string ReleaseAuthority, string Rationale) : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
