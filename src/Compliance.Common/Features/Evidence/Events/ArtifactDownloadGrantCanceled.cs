using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Fences one receiving artifact download grant before source-use settlement.</summary>
[Discriminator("bdgrz.artifact-download-grant.artifact-download-grant-canceled", 1)]
public sealed record ArtifactDownloadGrantCanceled(
    EventStreamAddress Owner,
    long Revision,
    long Generation,
    ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs,
    OperationProvenance Operation,
    Uuid GrantId,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent;
