using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Holds the exact active engagement-partner designation the partner evaluation relies on; the use does not advance the public revision, so <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-authorized", 1)]
public sealed record FirmProfessionalDutyDesignationEngagementUseAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
