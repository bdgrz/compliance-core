using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     A pending amendment ended before it applied, by explicit cancellation or by a proven calendar conflict. The
///     fact is the local receiver fence: a delayed apply sees it and does nothing, and only then is the pending
///     calendar reservation released.
/// </summary>
[Discriminator("bdgrz.type-ii-period.type-ii-period-amendment-canceled", 1)]
public sealed record TypeIIPeriodAmendmentCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ProgramId) : DomainEvent, INativeOperationFact;
