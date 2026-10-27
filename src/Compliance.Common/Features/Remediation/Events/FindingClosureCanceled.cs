using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     A pending closure ended before it closed the finding. The fact is the local receiver fence: a delayed
///     <c>CompleteFindingClosure</c> sees it and does nothing, and only then are the action uses settled.
///     <paramref name="SourceProofs" /> is the failure proof.
/// </summary>
[Discriminator("bdgrz.finding-aggregate.finding-closure-canceled", 1)]
public sealed record FindingClosureCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
