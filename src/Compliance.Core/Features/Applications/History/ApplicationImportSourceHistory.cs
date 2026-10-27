using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.History;

/// <summary>Complete transient admission, terminal-fence and publication history at the actual hydrated source cut.</summary>
public static class ApplicationImportSourceHistory
{
    public static async Task<ApplicationImportSourceHistorySnapshot> ReadAsync(IDomainEventReader events,
        ApplicationImportSource owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var logical = new HashSet<Uuid>();
        var slots = new Dictionary<Uuid, ApplicationImportSourceSlot>();
        var published = new List<ApplicationImportSourceSettled>();
        var digest = ApplicationImportSourceDigest.Empty;
        Uuid? active = null;
        long revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("The import source prefix contains another owner or repeated logical admission.");
            digest = ApplicationImportSourceDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected source acceptance differs from the exact native payload and metadata.");
            var operation = native.Operation;
            var batchId = fact switch
            {
                ApplicationImportSourceAdmissionRecorded value => value.BatchId,
                ApplicationImportSourceSettled value => value.BatchId,
                _ => Uuid.Empty,
            };
            if (batchId == Uuid.Empty || operation.RootOperationId == Uuid.Empty || operation.CausationEventId is null ||
                operation.FirstOwner != new ApplicationImportBatch(owner.TenantId, batchId).Stream ||
                operation.AffectedOwner != owner.Stream || operation.ExpectedRevisionOwner != owner.Stream ||
                operation.Episode != "initial" || operation.AuthoredIntentSha256.Length != 64 ||
                !operation.AuthoredIntentSha256.All(Uri.IsHexDigit))
                throw Invalid("The source fact differs from its exact batch first-owner native lineage.");
            var slot = slots.GetValueOrDefault(batchId);
            switch (fact)
            {
                case ApplicationImportSourceAdmissionRecorded admitted:
                    if (admitted.Owner != owner.Stream || admitted.Revision != revision + 1 ||
                        admitted.Generation != ApplicationImportEffects.Generation || admitted.SourceKey != owner.SourceKey ||
                        admitted.SourceNamespace != owner.SourceNamespace || active is not null || slot is not null ||
                        operation.CommandKind != nameof(ClaimApplicationImportSource))
                        throw Invalid("A source admits exactly one unfenced batch at its next revision.");
                    slots.Add(batchId, new(batchId, admitted, null));
                    active = batchId;
                    break;
                case ApplicationImportSourceSettled settled:
                    if (settled.Owner != owner.Stream || settled.Revision != revision + 1 ||
                        settled.Generation != ApplicationImportEffects.Generation || slot?.Settled is not null ||
                        settled.Disposition == ApplicationImportDisposition.Published && slot?.Admitted is null ||
                        !Enum.IsDefined(settled.Disposition) || operation.CommandKind != nameof(ReleaseApplicationImportSource))
                        throw Invalid("A source settles one exact batch once; publication requires its prior admission.");
                    slots[batchId] = new(batchId, slot?.Admitted, settled);
                    if (active == batchId)
                        active = null;
                    if (settled.Disposition == ApplicationImportDisposition.Published)
                        published.Add(settled);
                    break;
                default:
                    throw Invalid("The source prefix contains another native fact kind.");
            }
            revision++;
        }
        if (owner.Revision != revision || owner.ActiveBatchId != active ||
            !Same(owner.Find(owner.SelectedBatchId), slots.GetValueOrDefault(owner.SelectedBatchId)) ||
            active is { } held && !Same(owner.Find(held), slots.GetValueOrDefault(held)) ||
            (owner.Latest is null) != (facts.Count == 0) || facts.Count != 0 &&
                ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()) != ContentDigest.Sha256(facts[^1], facts[^1].GetType()))
            throw Invalid("The complete source prefix differs from compact current/selected authority.");
        owner.NativeCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new(owner, facts, slots, published);
    }

    static bool Same(ApplicationImportSourceSlot? actual, ApplicationImportSourceSlot? expected) =>
        actual == expected && actual?.Admitted?.Metadata == expected?.Admitted?.Metadata &&
        actual?.Settled?.Metadata == expected?.Settled?.Metadata;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
