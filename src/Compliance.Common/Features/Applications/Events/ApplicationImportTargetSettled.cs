using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The target hold ends with the batch's exact terminal fact. A published disposition promotes the frozen
///     candidate to the application's content without a second revision increment; a canceled or failed disposition
///     discards it. <paramref name="Revision" /> is the published content revision after the settlement.
/// </summary>
[Discriminator("bdgrz.declared_application.import_target_settled", 1)]
public sealed record ApplicationImportTargetSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid BatchId, Uuid ApplicationId,
    ApplicationImportDisposition Disposition) : DomainEvent, INativeOperationFact;
