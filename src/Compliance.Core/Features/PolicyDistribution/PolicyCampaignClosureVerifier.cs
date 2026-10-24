using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Verifies a receiver's proven source: the frozen closure intent and its complete participant requirements are read
///     from verified immutable content, and the accepted closure fact (or its terminal fact) is found on the
///     authoritative campaign owner. The source reference only names the fact; nothing the caller states is trusted
///     until the owner confirms it.
/// </summary>
sealed class PolicyCampaignClosureVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>Verifies the accepted closure fact named by <paramref name="source" />.</summary>
    public async ValueTask<PolicyCampaignClosure> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var loaded = await LoadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return loaded;
        var accepted = loaded.Campaign!.ClosureFor(operationId);
        if (accepted is null)
            return PolicyCampaignClosure.Failed(Transient("The accepted closure is not visible yet."));
        return source.Stream != loaded.Campaign.Stream || accepted.Metadata.EventId != source.EventId ||
               accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference
            ? PolicyCampaignClosure.Failed(Permanent("The source is not the accepted closure of this campaign."))
            : loaded with { Accepted = accepted };
    }

    /// <summary>Verifies the terminal closed or canceled fact named by <paramref name="source" />.</summary>
    public async ValueTask<PolicyCampaignClosure> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var loaded = await LoadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return loaded;
        var campaign = loaded.Campaign!;
        var accepted = campaign.ClosureFor(operationId);
        if (accepted is null)
            return PolicyCampaignClosure.Failed(Transient("The accepted closure is not visible yet."));
        DomainEvent? terminal = campaign.Closing is { } closed && closed.Operation.RootOperationId == operationId
            ? closed
            : campaign.CanceledFor(operationId);
        if (terminal is null)
            return PolicyCampaignClosure.Failed(Transient("Neither the closed campaign nor the cancellation is visible yet."));
        return source.Stream != campaign.Stream || terminal.Metadata.EventId != source.EventId ||
               terminal.Metadata.AggregateVersion != source.AggregateVersion
            ? PolicyCampaignClosure.Failed(Permanent("The source is not the terminal fact of this closure."))
            : loaded with { Accepted = accepted };
    }

    async ValueTask<PolicyCampaignClosure> LoadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        var tenantId = PolicyCampaignContent.ParseTenant(source.Stream.Realm);
        ClosureIntent intent;
        IReadOnlyList<ClosureParticipant> participants;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(content, tenantId, intentReference, ct)
                .ConfigureAwait(false);
            participants = await ClosureParticipant.ReadAsync(content, tenantId, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return PolicyCampaignClosure.Failed(Permanent($"The frozen closure cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != operationId || intent.ParentKind != ClosureParentKind.PolicyCampaign ||
            source.Stream != intent.Header.FirstOwner)
            return PolicyCampaignClosure.Failed(Permanent("The command differs from the frozen closure intent."));
        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, intent.ParentId, commandId),
            ct).ConfigureAwait(false);
        return campaign.Stream != intent.Header.FirstOwner
            ? PolicyCampaignClosure.Failed(Permanent("The closure names another campaign stream."))
            : new PolicyCampaignClosure(campaign, null, intent, participants, null);
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
