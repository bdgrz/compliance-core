using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     Holds the exact draft rule version the partner evaluation relies on; the use does not advance the public revision, so <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-authorized", 1)]
public sealed record IndependenceRuleCatalogEngagementUseAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
