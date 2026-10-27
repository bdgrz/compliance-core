using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Holds the exact ratification of the rule version the partner evaluation relies on; the use does not advance the public revision, so <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-authorized", 1)]
public sealed record IndependenceRuleRatificationCatalogEngagementUseAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
