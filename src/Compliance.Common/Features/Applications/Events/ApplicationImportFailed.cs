using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>A terminal failure of a batch. Existing native business fact, rehomed on the batch.</summary>
[Discriminator("bdgrz.application_import.failed", 2)]
public sealed record ApplicationImportFailed(Uuid TenantId, string SourceKey, string SourceNamespace,
    Uuid BatchId, long Revision, string PlanSha256, Uuid? RowId, string FailureCode,
    DateTimeOffset FailedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
