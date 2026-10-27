using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Records the receiving grant for one protected evidence artifact download.</summary>
[Discriminator("bdgrz.evidence.evidence-artifact-download-issued", 2)]
public sealed record EvidenceArtifactDownloadIssuedV2(
    Uuid TenantId,
    Uuid ArtifactId,
    long Revision,
    Uuid RequestId,
    string ContentSha256,
    long ContentLength,
    string Purpose,
    ActorReference IssuedBy,
    DateTimeOffset IssuedAt,
    OperationProvenance Operation,
    ImmutableContentRef Intent,
    Uuid GrantId,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent;
