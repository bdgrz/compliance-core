using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.NamedRecords.Processes;

/// <summary>
///     What the root-fact reactor of a name-change process does: freeze the claims the change needs, with the names
///     they claim, as the process record's targets, and start and drive the ordered process. When the process record
///     already exists the targets are not recomputed, so replaying an old accepted fact never reads the record's
///     history again.
/// </summary>
public sealed class NamedRecordReactions(ProcessRootReaction process, IProcessRecordStore records,
    IImmutableContentStore content, IAggregateReader reader)
{
    /// <summary>Starts the kind's process for an accepted change and drives it once.</summary>
    public async ValueTask StartAsync(NamedRecordKindDescriptor kind, IReactorContext context, INamedRecordFact fact,
        string worker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fact);
        var realm = context.Source.Stream.Realm;
        var key = ProcessKey.ForRoot(kind.ProcessName, fact.Operation.RootOperationId,
            NativeFactReference.Of(context.Source.Stream, (DomainEvent)fact));
        var targets = await records.ReadAsync(key, ct).ConfigureAwait(false) is null
            ? await FreezeTargetsAsync(kind, realm, fact, ct).ConfigureAwait(false)
            : [];
        await process.StartAsync(kind.ProcessName, context, fact.Intent, targets, worker, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     The claim targets a change needs, each carrying its stream resource, name kind and normalized name: the new
    ///     claim from the authored content and the old claim from the revision the change expects.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> FreezeTargetsAsync(NamedRecordKindDescriptor kind, string realm,
        INamedRecordFact fact, CancellationToken ct = default)
    {
        var tenantId = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var intent = await content.ReadJsonAsync<RecordNameChangeIntent>(realm, fact.Intent, ct).ConfigureAwait(false);
        var targets = new List<string>();
        if (intent.NewClaimKey is { } newKey)
        {
            var desired = kind.ReadName(await content.ReadVerifiedAsync(realm, intent.AuthoredContent, ct)
                .ConfigureAwait(false));
            targets.Add(NamedRecordProcessView.EncodeTarget(newKey, desired.NameKind,
                desired.NormalizedName ?? throw new InvalidOperationException("A new claim key needs a name.")));
        }

        if (intent.OldClaimKey is { } oldKey && oldKey != intent.NewClaimKey)
        {
            // The record cannot have moved: only this process applies the change.
            var record = await reader.HydrateAsync(kind.OpenRecord(tenantId, intent.RecordId, Uuid.Empty), ct)
                .ConfigureAwait(false);
            var previous = record.At((long)intent.ExpectedRecordRevision)
                ?? throw new InvalidOperationException("The record no longer holds the revision this change expects.");
            var prior = kind.ReadName(await content.ReadVerifiedAsync(realm, previous.Content, ct)
                .ConfigureAwait(false));
            targets.Add(NamedRecordProcessView.EncodeTarget(oldKey, prior.NameKind,
                prior.NormalizedName ?? throw new InvalidOperationException("An old claim key needs a name.")));
        }

        return targets;
    }
}
