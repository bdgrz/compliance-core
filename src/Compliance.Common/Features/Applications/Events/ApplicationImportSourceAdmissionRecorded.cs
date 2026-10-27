using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The source admits exactly one batch. <paramref name="Revision" /> is the source's logical revision after this
///     fact; the plan was frozen against the revision before it.
/// </summary>
[Discriminator("bdgrz.application_import_source.admission_recorded", 1)]
public sealed record ApplicationImportSourceAdmissionRecorded(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid BatchId, string SourceKey,
    string SourceNamespace) : DomainEvent, INativeOperationFact;
