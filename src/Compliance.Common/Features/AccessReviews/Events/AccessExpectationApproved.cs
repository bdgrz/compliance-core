using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>A proposed expectation was approved.</summary>
[Discriminator("bdgrz.access_expectation.approved", 2)]
public sealed record AccessExpectationApproved(Uuid TenantId, Uuid SystemInstanceId, long Revision,
    Uuid ExpectationId, ActorReference ApprovedBy, DateTimeOffset ApprovedAt,
    Uuid? SeparationOfDutiesWaiverId, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
