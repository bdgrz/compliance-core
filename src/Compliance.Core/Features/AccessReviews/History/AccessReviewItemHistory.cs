using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.History;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.History;

/// <summary>Verifies complete native business/use chronology at the actual hydrated item prefix.</summary>
public static class AccessReviewItemHistory
{
    public static async Task<AccessReviewItemHistorySnapshot> ReadAsync(IDomainEventReader events,
        AccessReviewItem owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var digest = AccessReviewReplayDigest.Empty;
        var logical = new HashSet<Uuid>();
        var business = new List<DomainEvent>();
        var provisional = new Dictionary<Uuid, AccessDecisionsRecorded>();
        var fences = new Dictionary<Uuid, AccessReviewItemDecisionCanceled>();
        var published = new HashSet<Uuid>();
        var uses = new SourceUseHistoryValidation(owner.Stream);
        AccessReviewItemOpened? opened = null;
        AccessDecisionsRecorded? live = null;
        DomainEvent? latest = null;
        DomainEvent? selectedPin = null;
        long pinRevision = 0;
        var bulkTail = false;
        var closed = false;
        long revision = 0;
        var campaign = AccessReviewEffects.CampaignStream(owner.TenantId, owner.CampaignId);
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("The item history contains another owner or repeated logical operation.");
            digest = AccessReviewReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected item acceptance differs from its exact native payload and complete metadata.");
            var operation = native.Operation;
            uses.ObserveOwnerFact(fact);
            if (SourceUseFactKinds.Classify(fact) is { } kind)
            {
                var source = (ISourceUseFact)fact;
                var scope = UseScope(fact);
                if (opened is null || source.Owner != owner.Stream || source.Generation != AccessReviewEffects.Generation ||
                    !Scope(scope.Tenant, scope.Campaign, scope.Item) || scope.Revision != revision ||
                    operation.FirstOwner != campaign || operation.AffectedOwner != owner.Stream ||
                    operation.ExpectedRevisionOwner != owner.Stream ||
                    kind.Purpose is not (SourceUsePurpose.BulkDecision or SourceUsePurpose.CampaignCompletion))
                    throw Invalid("The item use differs from its exact scope, parent generation or current business revision.");
                if (kind.Granted && (closed || uses.Active.Count != 0 ||
                    kind.Purpose == SourceUsePurpose.BulkDecision && fences.ContainsKey(operation.RootOperationId)))
                    throw Invalid("The item cannot grant a bulk hold after its fence or while another operation holds it.");
                uses.Observe(kind.Purpose, kind.Granted, source);
                if (fact is AccessReviewItemDecisionSettled settled)
                {
                    if (settled.Disposition == SourceUseSettlementDisposition.IrreversibleDecision)
                    {
                        if (!provisional.ContainsKey(operation.RootOperationId) || fences.ContainsKey(operation.RootOperationId) ||
                            !published.Add(operation.RootOperationId))
                            throw Invalid("A batch publishes its exact provisional decision once and never after a fence.");
                    }
                    if (live?.Operation.RootOperationId == operation.RootOperationId)
                        live = null;
                }
                if (fact is AccessReviewItemCampaignCompletionUseSettled
                    { Disposition: SourceUseSettlementDisposition.ParentClosed })
                    closed = true;
                continue;
            }
            if (fact is AccessReviewItemDecisionCanceled fence)
            {
                if (opened is null || !Scope(fence.TenantId, fence.CampaignId, fence.ItemId) || fence.Owner != owner.Stream ||
                    fence.Revision != revision || fence.Generation != AccessReviewEffects.Generation ||
                    operation.FirstOwner != campaign || operation.RootOperationId == Uuid.Empty ||
                    published.Contains(operation.RootOperationId) || !fences.TryAdd(operation.RootOperationId, fence))
                    throw Invalid("The item fence has another batch, scope, revision or irreversible publication.");
                continue;
            }
            bool valid;
            switch (fact)
            {
                case AccessReviewItemOpened value:
                    valid = opened is null && revision == 0 && Scope(value.TenantId, value.CampaignId, value.ItemId) &&
                        value.Owner == owner.Stream && value.Revision == 1 && value.Generation == AccessReviewEffects.Generation &&
                        operation.RootOperationId == owner.CampaignId && operation.FirstOwner == campaign &&
                        operation.CommandKind == nameof(MaterializeAccessReviewItem) && operation.CausationEventId is not null;
                    opened = value;
                    break;
                case AccessDecisionsRecorded decision:
                    valid = opened is not null && !closed && Scope(decision.TenantId, decision.CampaignId, decision.ItemId) &&
                        decision.Revision == revision + 1;
                    if (decision.DecisionBatchId is { } batch)
                    {
                        valid = valid && decision.PublicationRequired && operation.RootOperationId == batch &&
                            operation.FirstOwner == campaign && !fences.ContainsKey(batch) && live is null &&
                            uses.Active.TryGetValue(SourceUseSlot.Of(owner.Stream, owner.BulkKey(batch)), out var hold) && hold.IsActive &&
                            provisional.TryAdd(batch, decision);
                        live = decision;
                    }
                    else
                        valid = valid && !decision.PublicationRequired && uses.Active.Count == 0 &&
                            NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RecordAccessDecision), (ulong)revision);
                    break;
                case AccessRemediationChangeRecorded change:
                    valid = Root(change.TenantId, change.CampaignId, change.ItemId, change.Revision,
                        nameof(RecordAccessRemediationChange), operation);
                    break;
                case AccessRemediationVerified verified:
                    valid = Root(verified.TenantId, verified.CampaignId, verified.ItemId, verified.Revision,
                        nameof(VerifyAccessRemediation), operation);
                    break;
                case AccessRemediationExceptionRecorded exception:
                    valid = Root(exception.TenantId, exception.CampaignId, exception.ItemId, exception.Revision,
                        nameof(ExemptAccessRemediation), operation);
                    break;
                case AccessReviewResponsibilityReassigned reassigned:
                    valid = Root(reassigned.TenantId, reassigned.CampaignId, reassigned.ItemId, reassigned.Revision,
                        nameof(ReassignAccessReviewResponsibility), operation);
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The item prefix has another scope, business revision, source hold or native predecessor.");
            revision++;
            latest = fact;
            business.Add(fact);
            if (fact.Metadata.EventId == owner.SelectedContentEventId)
            {
                selectedPin = fact;
                pinRevision = revision;
                bulkTail = true;
            }
            else if (selectedPin is not null && fact is not AccessDecisionsRecorded { DecisionBatchId: not null })
                bulkTail = false;
        }
        if (revision != owner.Revision || closed != owner.IsClosed || owner.LiveProvisional != live || opened?.Metadata.EventId != owner.Opened?.Metadata.EventId ||
            (latest is null) != (owner.Latest is null) || latest is not null &&
                ContentDigest.Sha256(latest, latest.GetType()) != ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()) ||
            owner.ProvisionalFor(owner.SelectedBatchId) != provisional.GetValueOrDefault(owner.SelectedBatchId) ||
            owner.FenceFor(owner.SelectedBatchId) != fences.GetValueOrDefault(owner.SelectedBatchId) ||
            owner.IsPublished(owner.SelectedBatchId) != published.Contains(owner.SelectedBatchId) ||
            selectedPin?.Metadata.EventId != owner.SelectedPin?.Metadata.EventId || pinRevision != owner.SelectedPinRevision ||
            bulkTail != owner.OnlyBulkAfterPin)
            throw Invalid("The verified item prefix differs from compact current/selected authority.");
        try
        {
            owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        }
        catch (InvalidOperationException error)
        {
            throw Invalid($"The exact item source-use history cannot be verified: {error.Message}");
        }
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new(owner, facts, business, provisional, fences, published);

        bool Scope(Uuid tenant, Uuid atCampaign, Uuid item) => tenant == owner.TenantId && atCampaign == owner.CampaignId && item == owner.ItemId;
        bool Root(Uuid tenant, Uuid atCampaign, Uuid item, long next, string kind, OperationProvenance operation) =>
            opened is not null && !closed && uses.Active.Count == 0 && Scope(tenant, atCampaign, item) && next == revision + 1 &&
            NativeOwnerHistory.IsOwnRoot(owner, operation, kind, (ulong)revision);
    }

    static (Uuid Tenant, Uuid Campaign, Uuid Item, long Revision) UseScope(DomainEvent fact) => fact switch
    {
        AccessReviewItemDecisionAuthorized value => (value.TenantId, value.CampaignId, value.ItemId, value.Revision),
        AccessReviewItemDecisionSettled value => (value.TenantId, value.CampaignId, value.ItemId, value.Revision),
        AccessReviewItemCampaignCompletionUseAuthorized value => (value.TenantId, value.CampaignId, value.ItemId, value.Revision),
        AccessReviewItemCampaignCompletionUseSettled value => (value.TenantId, value.CampaignId, value.ItemId, value.Revision),
        _ => (Uuid.Empty, Uuid.Empty, Uuid.Empty, -1),
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
