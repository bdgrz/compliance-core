using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Retention.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Queries;

[Discriminator("bdgrz.artifact_retention.legal_holds.list", 1)]
public sealed record ListArtifactLegalHolds(Uuid TenantId, string SourceKind, Uuid SourceId, int? Limit = null,
    string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<ArtifactLegalHoldView>>, IArtifactRetentionAdminRequest, ICallable;
