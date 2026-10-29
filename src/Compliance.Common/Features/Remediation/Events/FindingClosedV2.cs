using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     The finding closed. <paramref name="ClosureContent" /> is the complete <see cref="FindingClosureView" /> and
///     <paramref name="ActionCompletionProofs" /> the complete manifest of the held completion facts of every
///     admitted action of <paramref name="FindingGeneration" />.
/// </summary>
[Discriminator("bdgrz.finding.closed", 3)]
public sealed record FindingClosedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    ImmutableContentRef ClosureContent, OperationProvenance Operation, long FindingGeneration,
    ImmutableContentRef ActionCompletionProofs, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
