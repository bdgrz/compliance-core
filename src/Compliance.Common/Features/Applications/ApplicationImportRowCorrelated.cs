using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A personal link-existing or create-new decision for one staged row.</summary>
[Discriminator("bdgrz.application_import.row_correlated", 2)]
public sealed record ApplicationImportRowCorrelated(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, Uuid RowId, string SourceRecordId,
    string Decision, Uuid ApplicationId, long? ExpectedApplicationRevision, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RecordedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
