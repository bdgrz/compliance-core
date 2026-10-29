using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Readers;

/// <summary>Bounded identity discovery only; a locator never establishes global mutation authority.</summary>
public interface IObservationPeriodCloseDirectoryReader
{
    ValueTask<IReadOnlyList<ObservationPeriodCloseIdentity>> FindAsync(Uuid tenantId, Uuid programId, Uuid closeId,
        CancellationToken ct = default);
}
