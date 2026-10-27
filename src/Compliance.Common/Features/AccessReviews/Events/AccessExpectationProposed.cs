using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     An expectation version was proposed on its expectation line. The logical id of <paramref name="Operation" />
///     is the expectation id; the complete <see cref="AccessExpectationView" /> is verified immutable content.
/// </summary>
[Discriminator("bdgrz.access_expectation.proposed", 2)]
public sealed record AccessExpectationProposed(Uuid TenantId, Uuid SystemInstanceId, long Revision,
    Uuid ProposerMemberId, ImmutableContentRef ExpectationContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
