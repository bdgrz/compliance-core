using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The materialization of an admitted corrective action on its own owner, at revision one. The complete
///     <see cref="CorrectiveActionView" /> is verified content and <paramref name="Intent" /> repeats the admission
///     intent the finding accepted.
/// </summary>
[Discriminator("bdgrz.finding.corrective_action.added", 3)]
public sealed record CorrectiveActionAddedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, ImmutableContentRef ActionContent, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
