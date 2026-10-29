using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Actual scoped predecessor and retained frozen Type II close sources; manual source fields remain declarations.</summary>
public sealed class AuditPopulationSources(IDomainEventReader events, ISourceFactResolver resolver, IImmutableContentStore content)
{
    internal async Task<ImmutableContentRef> FreezeDefinitionAsync(AuditPopulation owner, AuditPopulationView state,
        TypeIIPeriodReadSnapshot period, CancellationToken ct)
    {
        if (owner.Revision != 0 || owner.CommittedStreamPosition != 0 || period.View is not { } view ||
            view.Status != "closed" || view.CloseSnapshots.Count == 0 ||
            view.CloseSnapshots[^1].CloseSnapshotId != state.CloseSnapshot.CloseSnapshotId)
            throw Invalid("A population definition requires the exact current closed period and an empty owner.");
        var records = await RecordsAsync(period.Stream, period.Owner.CommittedStreamPosition, ct).ConfigureAwait(false);
        var close = records.FirstOrDefault(record => (record.Event is ObservationPeriodCloseFrozen or TypeIIPeriodCloseAmended) &&
            record.Event is ITypeIIPeriodStateFact fact && fact.Header.LatestCloseSnapshotId == state.CloseSnapshot.CloseSnapshotId)
            ?? throw new ImmutableContentUnavailableException("The exact frozen period close is not visible yet.");
        if (period.FactById(close.Event.Metadata.EventId) is not { } verified || verified.GetType() != close.Event.GetType() ||
            !verified.Equals(close.Event) || verified.Metadata != close.Event.Metadata)
            throw Invalid("The frozen close differs from the verified period history.");
        var actual = await ResolveAsync(close.Stream, close.ResourceOffset, ct).ConfigureAwait(false);
        Require(actual, close.Event, period.Stream);
        await VerifyCloseAsync(actual, state, ct).ConfigureAwait(false);
        return await NativeContent.WriteAsync(content, owner.TenantId, AuditPopulationDefinitionSource.Schema,
            new AuditPopulationDefinitionSource(actual.Locator, Metadata(actual.Event)), ct).ConfigureAwait(false);
    }

    internal async Task<TypeIIPeriodView> VerifyDefinitionAsync(AuditPopulation owner, AuditPopulationView state,
        ImmutableContentRef reference, CancellationToken ct)
    {
        if (reference is null || reference.Schema != AuditPopulationDefinitionSource.Schema)
            throw Invalid("The definition has another source proof schema.");
        var proof = await NativeContent.ReadAsync<AuditPopulationDefinitionSource>(content, owner.TenantId, reference, ct).ConfigureAwait(false);
        var stream = new EventStreamAddress(owner.TenantId.ToString(), TypeIIPeriod.Area, state.TypeIIPeriodId.ToString());
        if (proof.CloseFact is null || proof.CloseFact.Source != stream)
            throw Invalid("The definition close proof belongs to another period.");
        var actual = await ResolveAsync(proof.CloseFact.Source, proof.CloseFact.ResourcePosition, ct).ConfigureAwait(false);
        if (actual.Locator != proof.CloseFact || Metadata(actual.Event) != proof.CloseMetadataSha256)
            throw Invalid("The actual close differs from the retained definition proof.");
        await VerifyCloseAsync(actual, state, ct).ConfigureAwait(false);
        var fact = (ITypeIIPeriodStateFact)actual.Event;
        var period = await NativeContent.ReadAsync<TypeIIPeriodView>(content, owner.TenantId, fact.CurrentContent, ct).ConfigureAwait(false);
        if (period.Status != "closed" || period.CloseSnapshots.Count == 0 ||
            period.CloseSnapshots[^1].CloseSnapshotId != state.CloseSnapshot.CloseSnapshotId)
            throw Invalid("The definition did not bind the close at the frozen source cut.");
        return period;
    }

    public async Task<ImmutableContentRef> FreezeAsync(AuditPopulation owner, AuditPopulationView before,
        TypeIIPeriodReader periods, CancellationToken ct)
    {
        var prefix = await RecordsAsync(owner.Stream, owner.CommittedStreamPosition, ct).ConfigureAwait(false);
        var predecessor = prefix.LastOrDefault(record => !record.Event.Metadata.IsAudit)
            ?? throw Invalid("A native population mutation requires its exact existing predecessor.");
        var actual = await ResolveAsync(predecessor.Stream, predecessor.ResourceOffset, ct).ConfigureAwait(false);
        Require(actual, predecessor.Event, owner.Stream);
        var period = await periods.ReadAsync(owner.TenantId, owner.ProgramId, before.TypeIIPeriodId, ct).ConfigureAwait(false);
        if (period.Owner.ProgramId != owner.ProgramId)
            throw Invalid("The retained period source belongs to another actual program.");
        if (period.View is not { } periodView)
            throw new ImmutableContentUnavailableException("The retained Type II period source is not visible yet.");
        if (periodView.ExaminationId != before.ExaminationId)
            throw Invalid("The retained period source belongs to another actual examination.");
        var retainedClose = periodView.CloseSnapshots.FirstOrDefault(close => close.CloseSnapshotId == before.CloseSnapshot.CloseSnapshotId)
            ?? throw new ImmutableContentUnavailableException("The exact retained Type II close source is not visible yet.");
        if (ContentDigest.OfJson(retainedClose) != ContentDigest.OfJson(before.CloseSnapshot))
            throw Invalid("The population close differs from its actual canonical Type II close snapshot.");
        var closeRecords = await RecordsAsync(period.Stream, period.Owner.CommittedStreamPosition, ct).ConfigureAwait(false);
        var closes = closeRecords.Select(record => record.Event)
            .Where(fact => fact is ObservationPeriodCloseFrozen or TypeIIPeriodCloseAmended)
            .Where(fact => ((ITypeIIPeriodStateFact)fact).Header.LatestCloseSnapshotId == before.CloseSnapshot.CloseSnapshotId).ToArray();
        if (closes.Length == 0)
            throw new ImmutableContentUnavailableException("The actual frozen close source is not visible yet.");
        var closeFact = closes[0];
        if (period.FactById(closeFact.Metadata.EventId) is not { } verifiedClose || verifiedClose.GetType() != closeFact.GetType() ||
            !verifiedClose.Equals(closeFact) || verifiedClose.Metadata != closeFact.Metadata)
            throw Invalid("The discovered close source differs from the complete verified period read.");
        var closeRecord = closeRecords.SingleOrDefault(record => record.Event.Metadata.EventId == closeFact.Metadata.EventId)
            ?? throw new ImmutableContentUnavailableException("The actual frozen close resource offset is not visible yet.");
        var closeSource = await ResolveAsync(closeRecord.Stream, closeRecord.ResourceOffset, ct).ConfigureAwait(false);
        Require(closeSource, closeFact, period.Stream);
        await VerifyCloseAsync(closeSource, before, ct).ConfigureAwait(false);
        var cut = AuditPopulationPreparedCut.Of(owner);
        return await NativeContent.WriteAsync(content, owner.TenantId, AuditPopulationDecisionSource.Schema,
            new AuditPopulationDecisionSource(actual.Locator, Metadata(predecessor.Event), closeSource.Locator,
                Metadata(closeSource.Event), owner.Revision, cut.PhysicalPosition, cut.PayloadSha256, cut.MetadataSha256,
                ContentDigest.OfJson(before), before.CloseSnapshot.CloseSnapshotId, before.CloseSnapshot.ContentSha256), ct).ConfigureAwait(false);
    }

    public async Task VerifyAsync(AuditPopulation owner, AuditPopulationView before, DomainEvent predecessor,
        ulong position, string payloadPrefix, string metadataPrefix, ImmutableContentRef reference, CancellationToken ct)
    {
        if (reference is null || reference.Schema != AuditPopulationDecisionSource.Schema)
            throw Invalid("The population decision has another source proof schema.");
        var source = await NativeContent.ReadAsync<AuditPopulationDecisionSource>(content, owner.TenantId, reference, ct).ConfigureAwait(false);
        var closeStream = new EventStreamAddress(owner.TenantId.ToString(), TypeIIPeriod.Area, before.TypeIIPeriodId.ToString());
        if (source.Predecessor is null || source.CloseFact is null || source.Predecessor.Source != owner.Stream ||
            source.CloseFact.Source != closeStream || source.Predecessor.EventId == Uuid.Empty || source.CloseFact.EventId == Uuid.Empty ||
            source.Predecessor.AggregateVersion == 0 || source.CloseFact.AggregateVersion == 0 || source.Revision != before.Revision ||
            source.PhysicalPosition != position || source.PayloadPrefixSha256 != payloadPrefix || source.MetadataPrefixSha256 != metadataPrefix ||
            source.PredecessorContentSha256 != ContentDigest.OfJson(before) ||
            source.CloseSnapshotId != before.CloseSnapshot.CloseSnapshotId || source.CloseSnapshotSha256 != before.CloseSnapshot.ContentSha256 ||
            source.Predecessor.ResourcePosition >= position)
            throw Invalid("The decision source differs from its complete exact target predecessor cut.");
        var actual = await ResolveAsync(source.Predecessor.Source, source.Predecessor.ResourcePosition, ct).ConfigureAwait(false);
        if (actual.Locator != source.Predecessor || Metadata(actual.Event) != source.PredecessorMetadataSha256)
            throw Invalid("The actual predecessor source differs from its complete retained native locator or metadata.");
        Require(actual, predecessor, owner.Stream);
        var close = await ResolveAsync(source.CloseFact.Source, source.CloseFact.ResourcePosition, ct).ConfigureAwait(false);
        if (close.Locator != source.CloseFact || Metadata(close.Event) != source.CloseMetadataSha256)
            throw Invalid("The actual close source differs from its complete retained native locator or metadata.");
        await VerifyCloseAsync(close, before, ct).ConfigureAwait(false);
    }

    async Task VerifyCloseAsync(ResolvedSourceFact actual, AuditPopulationView before, CancellationToken ct)
    {
        var expected = new EventStreamAddress(before.TenantId.ToString(), TypeIIPeriod.Area, before.TypeIIPeriodId.ToString());
        if (actual.Locator.Source != expected || actual.Event is not (ObservationPeriodCloseFrozen or TypeIIPeriodCloseAmended) ||
            actual.Event is not ITypeIIPeriodStateFact fact || fact.TenantId != before.TenantId || fact.SubjectId != before.TypeIIPeriodId ||
            fact.CurrentContent.Schema != TypeIIPeriodContent.StateSchema || fact.Header.ProgramId != before.ProgramId ||
            fact.Header.ExaminationId != before.ExaminationId || fact.Header.LatestCloseSnapshotId != before.CloseSnapshot.CloseSnapshotId)
            throw Invalid("The population's close proof belongs to another actual period, program or examination.");
        var state = await NativeContent.ReadAsync<TypeIIPeriodView>(content, before.TenantId, fact.CurrentContent, ct).ConfigureAwait(false);
        if (TypeIIPeriodContent.Header(state) != fact.Header || ContentDigest.OfJson(state) != fact.CurrentContent.Sha256 ||
            !state.CloseSnapshots.Any(close => ContentDigest.OfJson(close) == ContentDigest.OfJson(before.CloseSnapshot)))
            throw Invalid("The actual retained close body differs from the population's complete immutable close binding.");
    }

    internal async Task<IReadOnlyList<DomainEventRecord>> RecordsAsync(EventStreamAddress stream, ulong through, CancellationToken ct)
    {
        var records = new List<DomainEventRecord>();
        var ids = new HashSet<Uuid>();
        ulong position = 0;
        ulong version = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (position == through)
                break;
            var metadata = record.Event.Metadata;
            if (record.Stream != stream || record.ResourceOffset != position || metadata.EventId == Uuid.Empty ||
                !ids.Add(metadata.EventId) || metadata.AggregateId.ToString() != stream.Resource ||
                !metadata.IsAudit && metadata.AggregateVersion != ++version)
                throw Invalid("The exact source has another raw physical/native prefix or metadata identity.");
            records.Add(record);
            position++;
        }
        return position == through ? records.AsReadOnly() :
            throw new ImmutableContentUnavailableException("The exact source prefix is not visible yet.");
    }
    async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong position, CancellationToken ct)
    {
        try
        { return await resolver.ResolveAsync(stream, position, ct).ConfigureAwait(false); }
        catch (SourceFactUnavailableException error) { throw new ImmutableContentUnavailableException(error.Message); }
        catch (SourceFactVerificationException error) { throw Invalid(error.Message); }
    }
    static void Require(ResolvedSourceFact source, DomainEvent expected, EventStreamAddress stream)
    {
        if (source.Locator.Source != stream || source.Locator.EventId != expected.Metadata.EventId ||
            source.Locator.AggregateVersion != expected.Metadata.AggregateVersion || source.Event.GetType() != expected.GetType() ||
            !source.Event.Equals(expected) || source.Event.Metadata != expected.Metadata ||
            source.Locator.PayloadSha256 != ContentDigest.Sha256(expected, expected.GetType()))
            throw Invalid("The actual native source differs from its full expected payload and metadata.");
    }
    static string Metadata(DomainEvent fact) => AuditPopulationReplayCut.Append(AuditPopulationReplayCut.Empty, fact, true);
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
