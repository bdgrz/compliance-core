using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.Applications.Services;

/// <summary>
///     Hydrates the governed application a single owner-local decision sees. A target's own stream replays only its
///     own facts: a provisional import effect never changes the published content, a publication shows when the
///     target's own settlement fact exists, and a canceled effect never advances the public revision. Responses that
///     compose several targets or can expose a batch publication that has not settled read through
///     <see cref="ApplicationPublicationReader" />, which fixes one global cut.
/// </summary>
static class ApplicationImportVisibility
{
    public static async ValueTask<DeclaredApplication> HydrateApplicationAsync(this IAggregateReader reader,
        Uuid tenantId, Uuid applicationId, CancellationToken ct = default) =>
        await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId), ct).ConfigureAwait(false);
}
