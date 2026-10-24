using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The use of the ratification catalog ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition.
/// </summary>
[Discriminator("bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-settled", 1)]
public sealed record IndependenceRuleRatificationCatalogEngagementUseSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
