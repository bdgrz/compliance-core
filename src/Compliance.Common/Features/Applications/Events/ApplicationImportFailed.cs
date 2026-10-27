using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A terminal failure of a batch. Existing native business fact, rehomed on the batch.</summary>
[Discriminator("bdgrz.application_import.failed", 2)]
public sealed record ApplicationImportFailed(Uuid TenantId, string SourceKey, string SourceNamespace,
    Uuid BatchId, long Revision, string PlanSha256, Uuid? RowId, string FailureCode,
    DateTimeOffset FailedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
