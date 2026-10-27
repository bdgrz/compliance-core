using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>A personal link-existing or create-new decision for one staged row.</summary>
[Discriminator("bdgrz.application_import.row_correlated", 2)]
public sealed record ApplicationImportRowCorrelated(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, Uuid RowId, string SourceRecordId,
    string Decision, Uuid ApplicationId, long? ExpectedApplicationRevision, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RecordedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
