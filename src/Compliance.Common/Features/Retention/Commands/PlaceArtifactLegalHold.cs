using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

[Discriminator("bdgrz.artifact_retention.legal_hold.place", 1)]
public sealed record PlaceArtifactLegalHold(Uuid TenantId, string SourceKind, Uuid SourceId, string ExpectedContentSha256,
    Uuid HoldId, long ExpectedRevision, string Reason) : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
