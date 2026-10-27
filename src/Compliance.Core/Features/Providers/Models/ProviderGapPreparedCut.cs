using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Aggregates;
namespace Bdgrz.Compliance.Features.Providers.Models;

/// <summary>The exact local native gap authority against which source and immutable preparation was performed.</summary>
sealed record ProviderGapPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static ProviderGapPreparedCut Capture(ProviderAssuranceGap owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);

    public bool Matches(ProviderAssuranceGap owner) => this == Capture(owner);

    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The native provider gap authority changed while immutable content was prepared.", true));
}
