using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Commands;

[Discriminator("bdgrz.artifact_retention.legal_hold.release", 1)]
public sealed record ReleaseArtifactLegalHold(Uuid CommandId, Uuid TenantId, string SourceKind, Uuid SourceId, string ExpectedContentSha256,
    Uuid HoldId, long ExpectedRevision, string Reason) : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
