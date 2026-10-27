using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The target fence of a canceled batch. It is recorded even when the hold was never acquired, and blocks every
///     delayed reserve or apply of that batch locally.
/// </summary>
[Discriminator("bdgrz.declared_application.import_effect_canceled", 1)]
public sealed record ApplicationImportEffectCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid BatchId, Uuid ApplicationId)
    : DomainEvent, INativeOperationFact;
