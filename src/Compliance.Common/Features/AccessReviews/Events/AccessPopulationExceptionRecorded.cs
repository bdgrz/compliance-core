using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An expiring exception for a missing population was approved. The logical id of
///     <paramref name="Operation" /> is the exception id; the complete <see cref="AccessPopulationExceptionView" />
///     is verified immutable content. Expiry is evaluated at authoritative read time and needs no further fact.
/// </summary>
[Discriminator("bdgrz.access_population.exception_recorded", 2)]
public sealed record AccessPopulationExceptionRecorded(Uuid TenantId, Uuid SystemInstanceId, long Revision,
    ImmutableContentRef ExceptionContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
