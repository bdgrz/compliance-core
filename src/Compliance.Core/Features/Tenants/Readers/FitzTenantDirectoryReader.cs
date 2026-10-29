using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Tenants.Readers;

/// <summary>
///     Serves the "TenantDirectory" projector, which is <see cref="WorkloadScope.Global" /> on
///     <see cref="EventStreamPattern.ForPattern(string, string)" />("bdgrz", "tenants"), so its
///     realm is the fixed "bdgrz" rather than a tenant ID. Writes join its batch transaction, and
///     query-side reads open their own read-only transaction on that same realm.
/// </summary>
sealed class FitzTenantDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, TenantDirectoryKeys.Route, "TenantDirectory"),
      ITenantDirectoryProjection,
      ITenantDirectoryReader
{
    const string Realm = "bdgrz";

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is TenantRegistered registered)
        {
            await TenantDirectorySchema.Directory.InsertAsync(
                Transaction,
                new TenantView(registered.TenantId, registered.Name, registered.Slug,
                    Status: "provisioning",
                    LegalName: registered.LegalName ?? registered.Name,
                    OperatorUserId: registered.CreatorIsAdministrator ? null : registered.OwnerUserId,
                    RequiresInvitation: registered.FirstAdministratorEmail is not null &&
                                        !registered.CreatorIsAdministrator,
                    RequiresActivation: registered.ActivationRequired ||
                                        registered.FirstAdministratorEmail is not null),
                ct).ConfigureAwait(false);
        }
        else
        {
            if (domainEvent is TenantSlugChanged changed)
            {
                var old = await TenantDirectorySchema.Directory.GetAsync(Transaction, changed.TenantId, ct)
                    .ConfigureAwait(false);
                if (old is not null)
                    await TenantDirectorySchema.Directory.ReplaceAsync(Transaction, old,
                        old with { Slug = changed.NewSlug }, ct).ConfigureAwait(false);
                return;
            }
            var tenantId = domainEvent switch
            {
                TenantSlugConfirmed ev => ev.TenantId,
                TenantSlugRejected ev => ev.TenantId,
                TenantSuspended ev => ev.TenantId,
                TenantSuspendedV2 ev => ev.TenantId,
                TenantReactivated ev => ev.TenantId,
                TenantReactivatedV2 ev => ev.TenantId,
                TenantOffboardingStarted ev => ev.TenantId,
                TenantOffboardingStartedV2 ev => ev.TenantId,
                TenantActivated ev => ev.TenantId,
                TenantActivatedV2 ev => ev.TenantId,
                _ => Uuid.Empty,
            };
            if (tenantId != Uuid.Empty)
            {
                var current = await TenantDirectorySchema.Directory.GetAsync(Transaction, tenantId, ct)
                    .ConfigureAwait(false);
                if (current is not null)
                {
                    var status = TenantDirectoryStatus.After(current, domainEvent);
                    if (status is not null)
                        await TenantDirectorySchema.Directory.ReplaceAsync(
                            Transaction, current, current with { Status = status }, ct).ConfigureAwait(false);
                }
            }
        }
    }

    public async ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await TenantDirectorySchema.Directory.GetAsync(tx, tenantId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await TenantDirectorySchema.Directory.QueryPrimaryAsync(tx, limit, cursor, ct)
            .ConfigureAwait(false);
    }
}
