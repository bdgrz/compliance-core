using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Checks that a service identity's owner is a recorded person or an active team.</summary>
static class ServiceIdentityOwners
{
    public static async ValueTask<RequestError?> ValidateAsync(IAggregateReader reader,
        Uuid tenantId, string ownerKind, Uuid ownerId, CancellationToken ct)
    {
        var exists = ownerKind switch
        {
            "person" => (await reader.HydrateAsync(new Person(tenantId, ownerId), ct)
                .ConfigureAwait(false)).IsCreated,
            "team" => (await reader.HydrateAsync(new Team(tenantId, ownerId), ct)
                .ConfigureAwait(false)).IsActive,
            _ => true,
        };
        return exists
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "The service identity owner must be a recorded person or an active team of this organization.");
    }
}
