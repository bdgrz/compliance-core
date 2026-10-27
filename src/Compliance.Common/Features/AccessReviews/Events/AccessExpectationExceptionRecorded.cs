using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     An expiring exception to an approved expectation was approved. The logical id of
///     <paramref name="Operation" /> is the command id, which is also the exception id; the complete
///     <see cref="AccessExpectationExceptionView" /> is verified immutable content.
/// </summary>
[Discriminator("bdgrz.access_expectation.exception_recorded", 2)]
public sealed record AccessExpectationExceptionRecorded(Uuid TenantId, Uuid SystemInstanceId, long Revision,
    ImmutableContentRef ExceptionContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
