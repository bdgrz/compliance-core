using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>The exact finalized target cut a receiver or root prepared before append.</summary>
sealed record IdentityOwnerPreparationCut(ulong Version, ulong PhysicalPosition, string? Digest, string? MetadataDigest)
{
    public static IdentityOwnerPreparationCut Of(Aggregate owner) => new(owner.Version, owner.CommittedStreamPosition,
        owner switch
        {
            UserIdentity identity => identity.ReplayCut.Digest,
            EmailAddress address => address.ReplayCut.Digest,
            _ => null,
        }, owner switch
        {
            UserIdentity identity => identity.ReplayCut.MetadataDigest,
            EmailAddress address => address.ReplayCut.MetadataDigest,
            _ => null,
        });

    public Result Require(Aggregate owner) => this == Of(owner)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The identity target changed after preparation; retry against its current finalized cut.", true));
}
