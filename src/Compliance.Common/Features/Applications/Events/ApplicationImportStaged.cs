using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The immutable staged observation of a source and the root native acceptance of its submission. The complete row
///     content is the immutable <paramref name="RowsManifest" />; <paramref name="RowCount" /> and
///     <paramref name="InvalidCount" /> summarize it for readers.
/// </summary>
[Discriminator("bdgrz.application_import.staged", 2)]
public sealed record ApplicationImportStaged(Uuid TenantId, Uuid BatchId,
    Uuid SubmissionId, string SourceKey, string SourceNamespace, string Coverage,
    string ContentSha256, ImmutableContentRef RowsManifest,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset SubmittedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, int RowCount, int InvalidCount)
    : DomainEvent, INativeOperationFact;
