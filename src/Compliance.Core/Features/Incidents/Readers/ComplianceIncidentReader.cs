using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
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
using Bdgrz.Compliance.Features.Incidents.Contracts;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Persistence;
using Bdgrz.Compliance.Features.Incidents.Projections;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Incidents.Readers;

/// <summary>Verifies active write gates independently of historical bodies; reconstructs complete chronology for scoped/as-of reads.</summary>
public sealed class ComplianceIncidentReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, INativeEventReceiptReader? receipts = null)
{
    public async ValueTask<ComplianceIncidentReadAuthority?> ReadAuthorityAsync(Uuid tenant, Uuid program, Uuid incident, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ComplianceIncident(tenant, program, incident), ct).ConfigureAwait(false);
        if (owner.Header is not { } h || h.ProgramId != program)
            return null;
        if (owner.CurrentContent is not { Schema: ComplianceIncidentContentStore.AuthoritySchema } reference)
            throw new ImmutableContentIntegrityException("The incident lacks its immutable current authority.");
        var authority = await content.ReadJsonAsync<ComplianceIncidentCurrentAuthority>(tenant.ToString(), reference, ct).ConfigureAwait(false);
        if (authority.Assessments is null || AuthoredIntentDigest.Compute(authority.Assessments) != h.AssessmentHistorySha256)
            throw new ImmutableContentIntegrityException("The current incident assessor identities differ from its native authority.");
        return new(owner, authority);
    }
    public async ValueTask<ComplianceIncidentReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid incident, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ComplianceIncident(tenant, program, incident), ct).ConfigureAwait(false);
        return await ReadHistoryAsync(owner, program, null, ct).ConfigureAwait(false);
    }
    public async ValueTask<ComplianceIncidentReadSnapshot> ReadThroughAsync(Uuid tenant, Uuid program, Uuid incident,
        DateTimeOffset recordedThrough, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ComplianceIncident(tenant, program, incident), ct).ConfigureAwait(false);
        return await ReadHistoryAsync(owner, program, recordedThrough, ct).ConfigureAwait(false);
    }
    public async ValueTask<ComplianceIncidentReadSnapshot> ReadCurrentAsync(Uuid tenant, Uuid program, Uuid incident, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ComplianceIncident(tenant, program, incident), ct).ConfigureAwait(false);
        return await ReadCurrentAsync(owner, program, ct).ConfigureAwait(false);
    }
    internal async ValueTask<ComplianceIncidentReadSnapshot> ReadCurrentAsync(ComplianceIncident owner, Uuid program, CancellationToken ct)
    {
        if (owner.Header is not { } h || owner.ProgramId != program)
            return new(owner, new ComplianceIncidentRules(owner.TenantId, program, owner.Id), null, []);
        if (owner.CurrentContent is not { Schema: ComplianceIncidentContentStore.AuthoritySchema } reference)
            throw new ImmutableContentIntegrityException("The incident lacks its immutable current authority.");
        var authority = await content.ReadJsonAsync<ComplianceIncidentCurrentAuthority>(owner.TenantId.ToString(), reference, ct).ConfigureAwait(false);
        var view = await ComposeCurrentAsync(h, authority, ct).ConfigureAwait(false);
        return new(owner, ComplianceIncidentRules.FromView(view, owner.TenantId, program, owner.Id, true), authority, []);
    }
    async ValueTask<ComplianceIncidentView> ComposeCurrentAsync(ComplianceIncidentAuthorityHeader h, ComplianceIncidentCurrentAuthority authority, CancellationToken ct)
    {
        if (authority is null || authority.VersionContent is null || authority.Assessments is null)
            throw new ImmutableContentIntegrityException("The incident current authority lacks its exact required components.");
        var version = await ReadAsync<ComplianceIncidentVersionView>(h.TenantId, authority.VersionContent, ComplianceIncidentContentStore.VersionSchema, ct).ConfigureAwait(false);
        var assessment = authority.AssessmentContent is { } a ? await ReadAsync<ComplianceIncidentImpactAssessmentView>(h.TenantId, a, ComplianceIncidentContentStore.AssessmentSchema, ct).ConfigureAwait(false) : null;
        var decision = authority.DecisionContent is { } d ? await ReadAsync<ComplianceIncidentSystemDescriptionDecisionView>(h.TenantId, d, ComplianceIncidentContentStore.DecisionSchema, ct).ConfigureAwait(false) : null;
        var resolution = authority.ResolutionContent is { } r ? await ReadAsync<ComplianceIncidentResolutionView>(h.TenantId, r, ComplianceIncidentContentStore.ResolutionSchema, ct).ConfigureAwait(false) : null;
        var view = new ComplianceIncidentView(h.TenantId, h.ProgramId, h.IncidentId, h.Status, h.Revision, h.CurrentVersionNumber, version,
            [version], assessment is null ? [] : [assessment], decision is null ? [] : [decision], resolution, h.ActorMemberId, h.ActorDisplay, h.UpdatedAt);
        if (!ComplianceIncidentContentStore.MatchesCurrent(h, authority, view))
            throw new ImmutableContentIntegrityException("The incident active components differ from its compact native authority.");
        if (resolution is not null)
            await VerifyCompletionsAsync(h.TenantId, h.ProgramId, resolution, ct).ConfigureAwait(false);
        return view;
    }
    Task<T> ReadAsync<T>(Uuid tenant, ImmutableContentRef? value, string schema, CancellationToken ct) where T : notnull => value is not null && value.Schema == schema
        ? content.ReadJsonAsync<T>(tenant.ToString(), value, ct)
        : throw new ImmutableContentIntegrityException("The incident component has another exact schema.");
    async ValueTask<ComplianceIncidentReadSnapshot> ReadHistoryAsync(ComplianceIncident owner, Uuid program, DateTimeOffset? through, CancellationToken ct)
    {
        var rules = new ComplianceIncidentRules(owner.TenantId, program, owner.Id);
        var facts = new List<DomainEvent>();
        if (owner.Header is null || owner.ProgramId != program)
            return new(owner, rules, null, facts);
        ComplianceIncidentCurrentAuthority? prior = null;
        ComplianceIncidentAuthorityHeader? priorHeader = null;
        IComplianceIncidentFact? last = null;
        var sequence = 0L;
        var observedSequence = 0L;
        var receiptPrefix = through is { } cutoff
            ? await new RecordedSourceReceiptCutFactory(events, receipts ??
                throw new ImmutableContentUnavailableException("The exact broker receipt authority is not configured for this recorded-through query."))
                .Create(owner.TenantId, cutoff).ReadAsync(owner.Stream, ct).ConfigureAwait(false)
            : null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(), ComplianceIncident.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not IComplianceIncidentFact fact || record.Stream != owner.Stream || fact.Header.ProgramId != program ||
                fact.TenantId != owner.TenantId || fact.SubjectId != owner.Id || fact.NativeSequence != observedSequence + 1)
                throw new ImmutableContentIntegrityException("The incident history has another exact native sequence, receipt cut or owner scope.");
            if (fact.NativeSequence > owner.NativeSequence)
                break;
            observedSequence = fact.NativeSequence;
            if (receiptPrefix is not null && !receiptPrefix.Includes(record.Event))
                continue;
            var mutation = await ReadAsync<ComplianceIncidentMutation>(owner.TenantId, fact.Change, ComplianceIncidentContentStore.ChangeSchema, ct).ConfigureAwait(false);
            var authority = await ReadAsync<ComplianceIncidentCurrentAuthority>(owner.TenantId, fact.CurrentContent, ComplianceIncidentContentStore.AuthoritySchema, ct).ConfigureAwait(false);
            var current = await ComposeCurrentAsync(fact.Header, authority, ct).ConfigureAwait(false);
            var payload = await ComplianceIncidentContentStore.ReadPayloadAsync(content, owner.TenantId, mutation.Payload, ct).ConfigureAwait(false);
            var frozen = new FrozenComplianceIncidentMutation(rules.Current is { } before ? ComplianceIncidentContentStore.CurrentOnly(before) : null, current,
                prior, authority, fact.CurrentContent, fact.Change, mutation, payload, fact.Header);
            if (fact.ChangeCount != 1 || mutation.Kind != fact.Operation.CommandKind || !ComplianceIncidentContentStore.Verify(frozen, priorHeader))
                throw new ImmutableContentIntegrityException("The incident immutable component differs from its actual selected local business transition.");
            if (ComplianceIncidentContentStore.Execute(rules, mutation.Kind, payload, current.ActorMemberId, current.ActorDisplay, current.UpdatedAt) is not null ||
                rules.Current is not { } composed || ContentDigest.OfJson(ComplianceIncidentContentStore.CurrentOnly(composed)) != ContentDigest.OfJson(current))
                throw new ImmutableContentIntegrityException("The reconstructed incident chronology differs from its verified current cut.");
            rules.ClearPendingChanges();
            prior = authority;
            priorHeader = fact.Header;
            last = fact;
            sequence = fact.NativeSequence;
            facts.Add(record.Event);
        }
        if (observedSequence != owner.NativeSequence)
            throw new ImmutableContentUnavailableException("The complete captured native incident source cut is not readable yet.");
        if (through is null && (sequence != owner.NativeSequence || last is null || last.Header != owner.Header || last.CurrentContent != owner.CurrentContent))
            throw new ImmutableContentUnavailableException("The complete exact incident native source cut is not readable yet.");
        return new(owner, rules, prior, facts.AsReadOnly());
    }
    async ValueTask VerifyCompletionsAsync(Uuid tenant, Uuid program, ComplianceIncidentResolutionView resolution, CancellationToken ct)
    {
        foreach (var completion in resolution.FollowUpCompletions)
        {
            var proof = completion.Proof;
            if (proof.Source.Realm != tenant.ToString() || proof.Source.Area != CorrectiveAction.Area || proof.RequiredFacts.Schema != SourceFactProofs.RequiredFactsSchema ||
                proof.EventKind != EventKind.Of(typeof(CorrectiveActionCompletedV2)) || proof.EventId == Uuid.Empty || proof.OwnerRevision == 0)
                throw new ImmutableContentIntegrityException("The incident resolution has another required native completion proof kind or tenant.");
            var locator = await content.ReadJsonAsync<SourceFactLocator>(tenant.ToString(), proof.RequiredFacts, ct).ConfigureAwait(false);
            if (locator.Source != proof.Source || locator.EventId != proof.EventId || locator.ResourcePosition != proof.PhysicalPosition ||
                locator.AggregateVersion != proof.OwnerRevision || locator.PayloadSha256 != proof.PayloadSha256 ||
                locator.GlobalPosition != proof.GlobalPosition || locator.RealmPosition != proof.RealmPosition)
                throw new ImmutableContentIntegrityException("The incident completion proof differs from its exact immutable admitted locator.");
            DomainEvent? exact = null;
            try
            {
                await foreach (var record in events.ReadAsync(proof.Source, proof.PhysicalPosition, ct).ConfigureAwait(false))
                {
                    if (record.ResourceOffset != proof.PhysicalPosition)
                        continue;
                    if (record.Stream != proof.Source)
                        throw new ImmutableContentIntegrityException("A completion resolved on another source stream.");
                    exact = record.Event;
                    break;
                }
            }
            catch (InvalidOperationException) { throw new ImmutableContentUnavailableException("The exact admitted corrective-action completion is not readable yet."); }
            if (exact is null)
                throw new ImmutableContentUnavailableException("The exact admitted corrective-action completion is not readable yet.");
            if (exact is not CorrectiveActionCompletedV2 fact || fact.Metadata.IsAudit || fact.Metadata.EventId != proof.EventId ||
                fact.Metadata.AggregateVersion != proof.OwnerRevision || ContentDigest.Sha256(fact, typeof(CorrectiveActionCompletedV2)) != proof.PayloadSha256 ||
                fact.TenantId != tenant || fact.ProgramId != program || proof.Source != new CorrectiveAction(tenant, program, fact.FindingId, fact.ActionId).Stream ||
                completion.WorkItemId != WorkCandidate.IdFor(fact.ActionId, WorkSource.CorrectiveAction))
                throw new ImmutableContentIntegrityException("The incident resolution is not bound to the exact committed completion in this program.");
        }
    }
}
