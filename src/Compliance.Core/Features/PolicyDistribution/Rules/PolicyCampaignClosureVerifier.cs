using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Rules;

/// <summary>
///     Verifies a receiver's proven source: the frozen closure intent and its complete participant requirements are read
///     from verified immutable content, and the accepted closure fact (or its terminal fact) is found on the
///     authoritative campaign owner. The source reference only names the fact; nothing the caller states is trusted
///     until the owner confirms it.
/// </summary>
sealed class PolicyCampaignClosureVerifier(IAggregateReader reader, IImmutableContentStore content,
    IDomainEventReader events)
{
    /// <summary>Verifies the accepted closure fact named by <paramref name="source" />.</summary>
    public async ValueTask<PolicyCampaignClosure> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var loaded = await ReadAcceptedAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        return loaded.Error is not null ? loaded : await PrepareAsync(loaded, ct).ConfigureAwait(false);
    }

    /// <summary>Resolves only the exact native acceptance, before any immutable preparation or receiver replay.</summary>
    public async ValueTask<PolicyCampaignClosure> ReadAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var loaded = await LoadNativeAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return loaded;
        var accepted = loaded.Accepted!;
        return accepted.Metadata.EventId != source.EventId || accepted.Metadata.AggregateVersion != source.AggregateVersion
            ? PolicyCampaignClosure.Failed(Permanent("The source is not the accepted closure of this campaign."))
            : loaded;
    }

    /// <summary>Verifies the terminal closed or canceled fact named by <paramref name="source" />.</summary>
    public async ValueTask<PolicyCampaignClosure> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var loaded = await LoadNativeAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
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
            : await PrepareAsync(loaded with { Accepted = accepted }, ct).ConfigureAwait(false);
    }

    async ValueTask<PolicyCampaignClosure> LoadNativeAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture, out var tenantId) ||
            !Uuid.TryParse(source.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture, out var campaignId) ||
            source.Stream.Area != PolicyDistributionCampaign.Area)
            return PolicyCampaignClosure.Failed(Permanent("The closure names another campaign stream."));
        var campaign = await new PolicyCampaignReader(reader, events).ReadAsync(tenantId, campaignId,
            commandId, selectedClosureRootId: operationId, ct: ct).ConfigureAwait(false);
        if (campaign.Stream != source.Stream)
            return PolicyCampaignClosure.Failed(Permanent("The closure names another campaign stream."));
        if (campaign.ClosureFor(operationId) is not { } accepted)
            return PolicyCampaignClosure.Failed(Transient("The accepted closure is not visible yet."));
        return accepted.Intent != intentReference
            ? PolicyCampaignClosure.Failed(Permanent("The command differs from the frozen closure intent."))
            : new PolicyCampaignClosure(campaign, accepted, null, null, null);
    }

    /// <summary>Binds the extra immutable source terms before deciding a receiver whose command contains them.</summary>
    public async ValueTask<PolicyCampaignClosure> BindAsync(PolicyCampaignClosure closure, CancellationToken ct)
    {
        var campaign = closure.Campaign!;
        var accepted = closure.Accepted!;
        ClosureIntent intent;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(content, campaign.TenantId, accepted.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return PolicyCampaignClosure.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The frozen closure cannot be verified: {exception.Message}",
                isTransient: exception is ImmutableContentUnavailableException));
        }
        var expected = PolicyCampaignContent.HeaderOf(accepted.Operation, campaign.Stream, campaign.Stream,
            accepted.Operation.AcceptedAt);
        return intent.Header != expected || intent.ParentKind != ClosureParentKind.PolicyCampaign ||
            intent.SourceUses is null || !PolicyCampaignHistory.Reference(intent.SourceUses.Content, SourceUseRequirement.Schema) ||
            intent.ParentId != campaign.Id || intent.ParentGeneration != (ulong)accepted.Generation
            ? PolicyCampaignClosure.Failed(Permanent("The command differs from the frozen closure intent."))
            : closure with { Intent = intent };
    }

    /// <summary>Verifies complete immutable feasibility after the required source terms and native receipt decision.</summary>
    public async ValueTask<PolicyCampaignClosure> PrepareAsync(PolicyCampaignClosure closure, CancellationToken ct)
    {
        var bound = closure.Intent is null ? await BindAsync(closure, ct).ConfigureAwait(false) : closure;
        if (bound.Error is not null)
            return bound;
        try
        {
            return bound with
            {
                Participants = await ReadParticipantsAsync(bound.Campaign!, bound.Accepted!, bound.Intent!, ct)
                .ConfigureAwait(false)
            };
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return PolicyCampaignClosure.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The frozen closure cannot be verified: {exception.Message}",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }

    async Task<IReadOnlyList<ClosureParticipant>> ReadParticipantsAsync(PolicyCampaignSnapshot campaign,
        PolicyCampaignClosureAccepted accepted, ClosureIntent intent, CancellationToken ct)
    {
        if (!PolicyCampaignHistory.Reference(intent.AuthoredDecision, PolicyCampaignContent.CloseCommandSchema) ||
            intent.AdmittedChildren is null || intent.SourceUses is null || intent.AdmittedChildren.ItemSchema != PolicyCampaignContent.AdmittedResultSchema ||
            intent.AdmittedChildren.Order != SelectionOrder.Authored ||
            !PolicyCampaignHistory.Reference(intent.AdmittedChildren.Content, PolicyCampaignContent.AdmittedResultSchema) ||
            intent.SourceUses.ItemSchema != SourceUseRequirement.Schema || intent.SourceUses.Order != SelectionOrder.CanonicalKey ||
            !PolicyCampaignHistory.Reference(intent.SourceUses.Content, SourceUseRequirement.Schema))
            throw new ImmutableContentIntegrityException("The closure manifests name another typed complete selection.");
        var admitted = await PolicyCampaignContent.ReadManifestAsync<CampaignAdmittedResult>(content, campaign.TenantId,
            intent.AdmittedChildren, ct).ConfigureAwait(false);
        var expected = campaign.Admitted.Where(value => value.Metadata.AggregateVersion < accepted.Metadata.AggregateVersion).ToArray();
        if (admitted.Count != expected.Length)
            throw new ImmutableContentIntegrityException("The closure does not cover every result admitted before its exact acceptance.");
        for (var index = 0; index < expected.Length; index++)
        {
            var value = admitted[index];
            var fact = expected[index];
            if (value is null || value.PersonId == Uuid.Empty || value.OperationId != fact.Operation.RootOperationId ||
                value.Admission != NativeFactReference.Of(campaign.Stream, fact) ||
                value.Kind != PolicyDistributionCampaign.KindOf(fact) ||
                PolicyCampaignParticipant.StreamOf(campaign.TenantId, campaign.Id, value.PersonId) != fact.Operation.AffectedOwner)
                throw new ImmutableContentIntegrityException("The closure admitted-result selection differs from its exact native acceptance prefix.");
        }
        var groups = admitted.GroupBy(value => PolicyCampaignParticipant.StreamOf(campaign.TenantId, campaign.Id, value.PersonId))
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var requirements = await PolicyCampaignContent.ReadManifestAsync<SourceUseRequirement>(content, campaign.TenantId,
            intent.SourceUses, ct).ConfigureAwait(false);
        var selected = new HashSet<EventStreamAddress>();
        var participants = new List<ClosureParticipant>();
        if (requirements.Count != groups.Count)
            throw new ImmutableContentIntegrityException("The closure does not hold every participant with an admitted result.");
        foreach (var requirement in requirements)
        {
            if (requirement is null || requirement.SourceOwner is null || !selected.Add(requirement.SourceOwner) ||
                !groups.TryGetValue(requirement.SourceOwner, out var results) ||
                requirement.ResourceRevision != (ulong)results.Length || requirement.ContentEvent != results[^1].Admission ||
                requirement.Purpose != SourceUsePurpose.CampaignClosure ||
                !PolicyCampaignHistory.Reference(requirement.RequiredFacts, PolicyCampaignContent.AdmittedResultsSchema))
                throw new ImmutableContentIntegrityException("The participant use differs from the complete exact admitted-result prefix.");
            var pinned = await PolicyCampaignContent.ReadAsync<IReadOnlyList<CampaignAdmittedResult>>(content,
                campaign.TenantId, requirement.RequiredFacts, ct).ConfigureAwait(false);
            if (!pinned.SequenceEqual(results))
                throw new ImmutableContentIntegrityException("The participant use omits or changes an exact admitted-result proof.");
            participants.Add(new ClosureParticipant(requirement, results[0].PersonId));
        }
        return participants;
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
