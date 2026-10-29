using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

/// <summary>
///     Verifies the proven source of a source-use receiver that serves both engagement processes. The staff record,
///     partner duty and rule catalog pairs hold their source for a personal decision and for an acceptance or staff
///     assignment alike; the operation named by the command decides which owner fact is the authoritative source.
/// </summary>
sealed class EngagementUseVerifier(IAggregateReader reader, EngagementPersonalDecisionVerifier decisions,
    EngagementAcceptanceVerifier acceptances)
{
    /// <summary>Derives the verified source of an acquire receiver, or the closed reason it is already moot.</summary>
    public async ValueTask<Result<SourceUseSource>> AuthorizeSourceAsync<TRequest>(TRequest request,
        string commandKind, CancellationToken ct)
        where TRequest : ISourceUseAuthorization, IEngagementPersonalDecisionUse
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await IsAcceptanceAsync(request.Source, request.EngagementId, request.OperationId, ct).ConfigureAwait(false))
            return await decisions.AuthorizeSourceAsync(request, commandKind, ct).ConfigureAwait(false);
        return await acceptances.AuthorizeAsync(request.Source, request.Intent, request.OperationId,
            request.EngagementId, request.CommandId, request.Target, request.Generation, commandKind,
            new PinnedUse(request.SourceContentEventId == Uuid.Empty ? null : request.SourceContentEventId,
                request.ExpectedContentRevision), ct).ConfigureAwait(false);
    }

    /// <summary>Derives the verified terminal source of a settle receiver and the disposition it requires.</summary>
    public async ValueTask<Result<SourceUseSource>> SettleSourceAsync<TRequest>(TRequest request, string commandKind,
        CancellationToken ct) where TRequest : ISourceUseSettlement, IEngagementPersonalDecisionUse
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await IsAcceptanceAsync(request.Source, request.EngagementId, request.OperationId, ct).ConfigureAwait(false))
            return await decisions.SettleSourceAsync(request, commandKind, ct).ConfigureAwait(false);
        return await acceptances.SettleAsync(request.Source, request.Intent, request.OperationId,
            request.EngagementId, request.CommandId, request.Target, request.Generation, commandKind, ct)
            .ConfigureAwait(false);
    }

    /// <summary>True when the operation named by a command is an acceptance or staff assignment of the engagement.</summary>
    async ValueTask<bool> IsAcceptanceAsync(NativeFactReference source, Uuid engagementId, Uuid operationId,
        CancellationToken ct)
    {
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenantId) ||
            tenantId == Uuid.Empty || source.Stream.Area != ServiceEngagementIndependence.Area ||
            !Uuid.TryParse(source.Stream.Resource, CultureInfo.InvariantCulture, out var streamEngagement))
            return false;
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId,
            engagementId == Uuid.Empty ? streamEngagement : engagementId, selectedRootId: operationId), ct).ConfigureAwait(false);
        return owner.OperationOf(operationId) is not null;
    }
}
