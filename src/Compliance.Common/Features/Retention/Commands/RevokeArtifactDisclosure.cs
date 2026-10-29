using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the revoke artifact disclosure intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.revoke-artifact-disclosure", 1)]
public sealed record RevokeArtifactDisclosure(Uuid TenantId, Uuid DisclosureId, Uuid ArtifactId,
    long ExpectedRevision, string Rationale) : IRequest, IArtifactDisclosureRequest, ICallable;
