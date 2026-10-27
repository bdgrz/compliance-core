using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The root native acceptance of a closure decision. It seals action admission for <c>Generation</c> and
///     references the frozen <c>ClosureIntent</c>; the finding only closes once <see cref="FindingClosedV2" /> follows.
/// </summary>
[Discriminator("bdgrz.finding-aggregate.finding-closure-accepted", 1)]
public sealed record FindingClosureAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
