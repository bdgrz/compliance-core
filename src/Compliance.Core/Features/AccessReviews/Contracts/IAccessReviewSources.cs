using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Contracts;

/// <summary>
///     The governed workforce and inventory facts access reviews correlate with. Each read fails
///     transiently rather than answering from a projection that has not reached its source.
/// </summary>
public interface IAccessReviewSources
{
    /// <summary>Lists the tenant's roster people.</summary>
    ValueTask<Result<IReadOnlyList<PersonView>>> ListPeopleAsync(Uuid tenantId,
        CancellationToken ct = default);

    /// <summary>Lists the lifecycle statuses of a person's work relationships.</summary>
    ValueTask<Result<IReadOnlyList<string>>> RelationshipStatusesAsync(Uuid tenantId,
        Uuid personId, CancellationToken ct = default);

    /// <summary>Reads a service identity with its ownership and expiry evaluated now.</summary>
    ValueTask<Result<ServiceIdentityView>> GetServiceIdentityAsync(Uuid tenantId,
        Uuid serviceIdentityId, CancellationToken ct = default);

    /// <summary>The member correlated with the application's access owner, if any.</summary>
    ValueTask<Result<Uuid?>> AccessOwnerMemberAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);
}
