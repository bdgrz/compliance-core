using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Readers;

/// <summary>Checks the complete actual preference prefix before a read, retry or scalar append.</summary>
public sealed class WorkPreferenceAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is not WorkDigestPreference preference)
            return owner;
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, preference, ct).ConfigureAwait(false);
        var view = new WorkDigestPreferenceView(true, null) { TimeZoneId = "UTC" };
        var logical = new HashSet<Uuid>();
        var digest = WorkPreferenceReplayCut.Empty;
        var metadata = WorkPreferenceReplayCut.Empty;
        var known = WorkPreferenceReplayCut.Empty;
        var nativeStarted = false;
        foreach (var fact in facts)
        {
            digest = WorkPreferenceReplayCut.Append(digest, fact);
            metadata = WorkPreferenceReplayCut.Append(metadata, fact, true);
            if (fact.Metadata.AggregateVersion <= preference.ReplayCut.KnownThroughVersion)
                known = WorkPreferenceReplayCut.Append(known, fact, true);
            switch (fact)
            {
                case WorkDigestPreferenceChanged legacy when !nativeStarted:
                    if (legacy.TenantId != preference.TenantId || legacy.MemberId != preference.Id ||
                        !WorkDigestSchedule.IsValidTimeZone(legacy.TimeZoneId))
                        throw Invalid();
                    view = new WorkDigestPreferenceView(legacy.EmailDigestEnabled, legacy.ChangedAt)
                    { TimeZoneId = WorkDigestSchedule.NormalizeTimeZoneId(legacy.TimeZoneId) };
                    break;
                case WorkDigestPreferenceRecorded native:
                    nativeStarted = true;
                    if (native.Operation is null || native.Preference is null || native.TenantId != preference.TenantId || native.MemberId != preference.Id ||
                        !WorkDigestSchedule.IsValidTimeZone(native.AuthoredTimeZoneId) ||
                        !logical.Add(native.Operation.LogicalId) || native.Operation.Actor.Kind != "member" ||
                        native.Operation.Actor.Id != preference.Id.ToString() ||
                        !NativeOwnerHistory.IsOwnRoot(preference, native.Operation, nameof(SetWorkDigestPreference), null) ||
                        native.Operation.AuthoredIntentSha256 != AuthoredIntentDigest.Compute(new SetWorkDigestPreference(
                            native.Operation.LogicalId, preference.TenantId, native.EmailDigestEnabled, native.AuthoredTimeZoneId)) ||
                        native.FirstResponse is null || native.OperationResult is not { Schema: "mutation-result-content.v1" })
                        throw Invalid();
                    var next = WorkDigestPreference.Decide(view, native.EmailDigestEnabled, native.AuthoredTimeZoneId, native.Operation.AcceptedAt);
                    if (next != native.Preference || native.NoChange != (next == view))
                        throw Invalid();
                    if (native.Operation.LogicalId == preference.Acceptance.SelectedLogicalId &&
                        (preference.Acceptance.SelectedFact is not { } selected || !selected.Equals(native) || selected.Metadata != native.Metadata))
                        throw Invalid();
                    view = next;
                    break;
                default:
                    throw Invalid();
            }
        }
        if (view != preference.Read())
            throw Invalid();
        preference.ReplayCut.Confirm(preference, digest, metadata, known);
        return owner;
    }
    static ImmutableContentIntegrityException Invalid() => new("The preference differs from its complete actual owner prefix.");
}
