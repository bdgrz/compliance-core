using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Evaluates whether non-human identities are unowned work as of today.</summary>
public sealed class ServiceIdentityOwnership(IWorkforceObservationDirectoryReader roster,
    IAggregateReader reader, WorkforceObservationReadConsistency consistency, TimeProvider clock)
{
    public async ValueTask<Result<IReadOnlyList<ServiceIdentityView>>> EvaluateAsync(Uuid tenantId,
        IReadOnlyList<ServiceIdentityView> identities, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(identities);
        var ready = await consistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<IReadOnlyList<ServiceIdentityView>>.Failure(ready.Error);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var evaluated = new List<ServiceIdentityView>(identities.Count);
        foreach (var identity in identities)
        {
            IReadOnlyList<string> statuses = [];
            var teamActive = false;
            if (identity.LifecycleStatus != "retired")
            {
                if (identity.OwnerKind == "person")
                    statuses = await roster.ListRelationshipStatusesAsync(tenantId,
                        identity.OwnerId, ct).ConfigureAwait(false);
                else
                    teamActive = (await reader.HydrateAsync(new Team(tenantId, identity.OwnerId),
                        ct).ConfigureAwait(false)).IsActive;
            }
            var reasons = ServiceIdentityAccountability.Evaluate(identity.LifecycleStatus,
                identity.ReviewBy, today, identity.OwnerKind, statuses, teamActive);
            evaluated.Add(identity with
            {
                Unowned = reasons.Count > 0,
                UnownedReasons = reasons,
                Expired = ServiceIdentityAccountability.IsExpired(identity.LifecycleStatus,
                    identity.ExpiresOn, today),
            });
        }
        return Result<IReadOnlyList<ServiceIdentityView>>.Success(evaluated);
    }
}
