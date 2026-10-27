using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
