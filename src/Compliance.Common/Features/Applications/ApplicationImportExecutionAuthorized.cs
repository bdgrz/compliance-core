using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The batch authorizes execution after its source claim: the target reservations, applications and publication
///     are driven by this fact. It carries the sealed plan's intent unchanged.
/// </summary>
[Discriminator("bdgrz.application_import.execution_authorized", 1)]
public sealed record ApplicationImportExecutionAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
