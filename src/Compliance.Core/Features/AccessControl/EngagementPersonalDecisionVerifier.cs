using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Verifies a receiver's proven source: the frozen decision intent is read from verified immutable content and
///     the admission is found on the authoritative engagement owner. The source reference only names the fact; nothing
///     the caller states is trusted until the owner confirms it.
/// </summary>
sealed class EngagementPersonalDecisionVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>
    ///     Loads and verifies the admitted decision named by a receiver command. <paramref name="sourceKind" /> states
    ///     which fact of the engagement owner the command's source must be; the source of a settlement is a child fact
    ///     on another owner, which the caller verifies itself.
    /// </summary>
    public async ValueTask<LoadedDecision> LoadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid decisionId, Uuid engagementId, Uuid selectedLogicalId, CancellationToken ct,
        DecisionSource sourceKind = DecisionSource.Admission)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (decisionId == Uuid.Empty || engagementId == Uuid.Empty ||
            !Uuid.TryParse(source.Stream.Realm, out var tenantId) || tenantId == Uuid.Empty)
            return LoadedDecision.Failed(Permanent("The receiver names another decision, engagement or tenant."));
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId,
            selectedLogicalId, decisionId), ct).ConfigureAwait(false);
        if (owner.DecisionOf(decisionId) is not { } decision)
            return LoadedDecision.Failed(Transient("The admitted personal decision is not visible yet."));
        var admitted = decision.Admitted;
        DomainEvent? expected = sourceKind switch
        {
            DecisionSource.Admission => admitted,
            DecisionSource.Authorization => decision.Authorized,
            _ => null,
        };
        if (sourceKind != DecisionSource.Child)
        {
            if (expected is null)
                return LoadedDecision.Failed(Transient("The authorization of the personal decision is not visible yet."));
            if (source.Stream != owner.Stream || expected.Metadata.EventId != source.EventId ||
                expected.Metadata.AggregateVersion != source.AggregateVersion)
                return LoadedDecision.Failed(Permanent("The source is not the expected fact of this personal decision."));
        }

        if (admitted.Intent != intentReference)
            return LoadedDecision.Failed(Permanent("The command differs from the frozen personal decision intent."));

        EngagementPersonalDecisionIntent intent;
        IReadOnlyList<SourceUseRequirement> requirements;
        try
        {
            intent = await content.ReadJsonAsync<EngagementPersonalDecisionIntent>(
                IndependenceContent.Realm(tenantId), intentReference, ct).ConfigureAwait(false);
            if (intent.Header is null || intent.RequiredSourceUses is null || intent.CompleteServiceManifest is null ||
                intent.AuthoredContent is null)
                return LoadedDecision.Failed(Permanent("The frozen personal decision is missing required immutable terms."));
            requirements = await content.ReadSelectionAsync<SourceUseRequirement>(IndependenceContent.Realm(tenantId),
                intent.RequiredSourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return LoadedDecision.Failed(exception is ImmutableContentUnavailableException
                ? Transient($"The frozen personal decision is unavailable: {exception.Message}")
                : Permanent($"The frozen personal decision cannot be verified: {exception.Message}"));
        }

        if (intentReference.Schema != EngagementPersonalDecisionIntent.Schema ||
            !EngagementIntentBinding.Matches(intent.Header, admitted.Operation) ||
            intent.ExpectedEngagementRevision != (ulong)admitted.Revision ||
            intent.Kind != (admitted.Operation.CommandKind == ServiceEngagementIndependence.ManagementKind
                ? EngagementPersonalDecisionKind.ManagementAcknowledgement : EngagementPersonalDecisionKind.PartnerEvaluation) ||
            intent.DecisionId != decisionId || intent.Header.RootOperationId != decisionId ||
            intent.EngagementId != engagementId || intent.Header.FirstOwner != owner.Stream ||
            intent.Header.AuthoredIntentSha256 != admitted.Operation.AuthoredIntentSha256)
            return LoadedDecision.Failed(Permanent("The command differs from the frozen personal decision intent."));
        return new LoadedDecision(owner, decision, intent, requirements, null);
    }

    /// <summary>Derives the verified source of an acquire receiver, or the closed reason it is already moot.</summary>
    public async ValueTask<Result<SourceUseSource>> AuthorizeSourceAsync<TRequest>(TRequest request,
        string commandKind, CancellationToken ct) where TRequest : ISourceUseAuthorization, IEngagementPersonalDecisionUse
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(request.Source, request.Intent, request.OperationId, request.EngagementId,
            request.CommandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<SourceUseSource>.Failure(loaded.Error);
        var (owner, decision, intent) = (loaded.Owner!, loaded.Decision!, loaded.Intent!);
        var requirement = EngagementPersonalDecisionEffects.RequirementOf(loaded.Requirements!, request.Target);
        if (requirement is null || request.Generation != EngagementPersonalDecisionEffects.Generation ||
            requirement.ContentEvent.EventId != request.SourceContentEventId ||
            requirement.ResourceRevision != request.ExpectedContentRevision ||
            requirement.Purpose != SourceUsePurpose.EngagementPersonalDecision)
            return Result<SourceUseSource>.Failure(Permanent(
                "The authorization differs from the frozen source use requirement."));
        var superseded = decision.Canceled is not null ? "The personal decision was canceled."
            : decision.Authorized is not null ? "The personal decision is already authorized."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            EngagementPersonalDecisionEffects.UseKey(request.OperationId, owner.Stream, request.Target), requirement,
            commandKind, null, superseded)
        {
            RequiresAbort = decision.Canceled is not null,
        });
    }

    /// <summary>Derives the verified terminal source of a settle receiver and the disposition it requires.</summary>
    public async ValueTask<Result<SourceUseSource>> SettleSourceAsync<TRequest>(TRequest request, string commandKind,
        CancellationToken ct) where TRequest : ISourceUseSettlement, IEngagementPersonalDecisionUse
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(request.Source, request.Intent, request.OperationId, request.EngagementId,
            request.CommandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<SourceUseSource>.Failure(loaded.Error);
        var (owner, decision, intent) = (loaded.Owner!, loaded.Decision!, loaded.Intent!);
        var requirement = EngagementPersonalDecisionEffects.RequirementOf(loaded.Requirements!, request.Target);
        if (requirement is null || request.Generation != EngagementPersonalDecisionEffects.Generation)
            return Result<SourceUseSource>.Failure(Permanent(
                "The settlement differs from the frozen source use requirement."));
        SourceUseSettlementDisposition? disposition = decision.Authorized is not null
            ? SourceUseSettlementDisposition.IrreversibleDecision
            : decision.Canceled is not null ? SourceUseSettlementDisposition.Canceled : null;
        return disposition is null
            ? Result<SourceUseSource>.Failure(Transient(
                "Neither the authorization nor the cancellation of the personal decision is visible yet."))
            : Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
                EngagementPersonalDecisionEffects.UseKey(request.OperationId, owner.Stream, request.Target),
                requirement, commandKind, disposition, null));
    }

    internal static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    internal static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);

    /// <summary>The tenant of the engagement a stream belongs to.</summary>
    internal static Uuid TenantOf(EventStreamAddress stream) =>
        Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);
}

/// <summary>Which fact the source of a receiver command must be.</summary>
enum DecisionSource
{
    /// <summary>The root admission on the engagement owner.</summary>
    Admission,

    /// <summary>The irreversible authorization on the engagement owner.</summary>
    Authorization,

    /// <summary>A child fact on another owner, which the caller verifies itself.</summary>
    Child,
}

/// <summary>A verified personal decision of an engagement owner, or the reason it cannot be verified.</summary>
sealed record LoadedDecision(ServiceEngagementIndependence? Owner, EngagementPersonalDecisionRecord? Decision,
    EngagementPersonalDecisionIntent? Intent, IReadOnlyList<SourceUseRequirement>? Requirements, RequestError? Error)
{
    public static LoadedDecision Failed(RequestError error) => new(null, null, null, null, error);
}
