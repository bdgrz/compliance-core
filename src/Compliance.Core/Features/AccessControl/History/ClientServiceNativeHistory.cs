using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
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
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.History;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.History;

/// <summary>Full transient client service/preview/use chronology at an exact hydrated native and physical cut.</summary>
public static class ClientServiceNativeHistory
{
    public static async Task<ClientServiceHistorySnapshot> ReadAsync(IDomainEventReader events,
        ClientServiceIndependence owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var services = new List<NonattestServiceRecorded>();
        var admissions = new List<ClientIndependenceEvaluationAdmitted>();
        var logical = new HashSet<Uuid>();
        var uses = new SourceUseHistoryValidation(owner.Stream);
        var digest = FirmIndependenceReplayDigest.Empty;
        ulong epoch = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("The client history contains another owner or repeated logical operation identity.");
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected client receipt differs from its exact payload and complete metadata.");
            uses.ObserveOwnerFact(fact);
            if (SourceUseFactKinds.Classify(fact) is { } kind)
            {
                var source = (ISourceUseFact)fact;
                var revision = fact switch
                {
                    ClientServicePersonalDecisionUseAuthorized value => value.Revision,
                    ClientServicePersonalDecisionUseSettled value => value.Revision,
                    EngagementServiceEpochAuthorized value => value.Revision,
                    EngagementServiceEpochSettled value => value.Revision,
                    _ => -1,
                };
                if (source.Owner != owner.Stream || revision != (long)epoch ||
                    kind.Purpose is not (SourceUsePurpose.EngagementPersonalDecision or SourceUsePurpose.EngagementAcceptance))
                    throw Invalid("A client service use names another owner, purpose or service epoch.");
                uses.Observe(kind.Purpose, kind.Granted, source);
                continue;
            }
            switch (fact)
            {
                case NonattestServiceRecorded value:
                    if (value.TenantId != owner.TenantId || value.OperationId != value.Operation.LogicalId ||
                        value.Revision != epoch + 1 || value.FirstResponse is null || value.OperationResult is null ||
                        uses.Active.Count != 0 || !NativeOwnerHistory.IsOwnRoot(owner, value.Operation, nameof(RecordNonattestService), epoch))
                        throw Invalid("A service must be its own root at the next unheld client service epoch.");
                    services.Add(value);
                    epoch++;
                    break;
                case ClientIndependenceEvaluationAdmitted value:
                    if (value.Owner != owner.Stream || value.Revision != (long)epoch || value.Generation != ClientServiceIndependence.Generation ||
                        value.FirstResponse is null || !NativeOwnerHistory.IsOwnRoot(owner, value.Operation, nameof(EvaluateClientIndependence), epoch))
                        throw Invalid("A historical evaluation must be its own exact root at the selected service epoch.");
                    admissions.Add(value);
                    break;
                default:
                    throw Invalid("The client stream contains an unsupported native fact.");
            }
        }
        if (epoch != owner.Epoch || services.LastOrDefault() != owner.LatestService ||
            (admissions.LastOrDefault()?.Operation.LogicalId ?? Uuid.Empty) != owner.LastAdmissionId ||
            admissions.SingleOrDefault(value => value.Operation.LogicalId == owner.SelectedEvaluationId) != owner.AdmissionOf(owner.SelectedEvaluationId))
            throw Invalid("The client history differs from compact current/selected authority.");
        try
        {
            owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        }
        catch (InvalidOperationException error)
        {
            throw Invalid($"The client service-use prefix cannot be verified: {error.Message}");
        }
        owner.ReplayCut.Confirm(owner, digest);
        return new ClientServiceHistorySnapshot(owner, services, admissions);
    }
    internal static async Task VerifyDecisionAsync(IDomainEventReader events, ClientIndependenceDecision owner, CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<ClientIndependenceEvaluated>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count == 0 && owner.Evaluated is null)
            return;
        if (facts.Count != 1 || owner.Evaluated is not { } value || facts[0] != value || facts[0].Metadata != value.Metadata ||
            !Uuid.TryParse(owner.Stream.Realm, out var tenant) || value.TenantId != tenant || value.OperationId != owner.EvaluationId ||
            value.Revision != 1 || value.OperationResult is null || value.Operation.Owner != owner.Stream ||
            value.Operation.RootOperationId != owner.EvaluationId || value.Operation.CausationEventId is null ||
            value.Operation.CommandKind != nameof(MaterializeClientIndependencePreview) ||
            value.Operation.FirstOwner != new ClientServiceIndependence(tenant).Stream ||
            value.Operation.AffectedOwner != owner.Stream || value.Operation.ExpectedRevisionOwner != owner.Stream)
            throw Invalid("The finite client preview differs from its exact committed native scope and receipt.");
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
