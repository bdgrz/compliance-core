using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The source claim of one batch ends with the batch's exact terminal fact. A published batch adds its claims to
///     the source; a settlement before any admission fences the batch against a late claim.
/// </summary>
[Discriminator("bdgrz.application_import_source.settled", 1)]
public sealed record ApplicationImportSourceSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid BatchId,
    ApplicationImportDisposition Disposition) : DomainEvent, INativeOperationFact;
