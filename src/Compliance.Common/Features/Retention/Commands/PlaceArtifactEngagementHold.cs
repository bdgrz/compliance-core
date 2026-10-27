using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the place artifact engagement hold intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.place-artifact-engagement-hold", 1)]
public sealed record PlaceArtifactEngagementHold(Uuid TenantId, Uuid HoldId, Uuid EngagementId, Uuid? ReportArtifactId, string Scope, string Authority, string Reason) : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
