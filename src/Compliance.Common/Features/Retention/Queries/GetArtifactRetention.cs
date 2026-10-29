using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Queries;

[Discriminator("bdgrz.artifact_retention.get", 1)]
public sealed record GetArtifactRetention(Uuid TenantId, string SourceKind, Uuid SourceId, long? MinimumRevision = null)
    : IRequest<ArtifactRetentionView>, IArtifactRetentionAdminRequest, ICallable;
