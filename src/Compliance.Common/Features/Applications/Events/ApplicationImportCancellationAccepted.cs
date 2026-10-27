using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The root native acceptance of a cancellation. It is the batch-local fence: a later commit of the batch is
///     refused. Every target is then fenced before any hold is released.
/// </summary>
[Discriminator("bdgrz.application_import.cancellation_accepted", 1)]
public sealed record ApplicationImportCancellationAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
