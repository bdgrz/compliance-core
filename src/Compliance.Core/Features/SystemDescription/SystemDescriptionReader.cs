using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Exact native-cut reconstruction of immutable narrative versions, decisions and historical bindings.</summary>
public sealed class SystemDescriptionReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<SystemDescriptionReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid descriptionId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SystemDescription(tenantId, programId, descriptionId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, programId, ct).ConfigureAwait(false);
    }
    internal async ValueTask<SystemDescriptionReadSnapshot> ReadCurrentAsync(SystemDescription owner, Uuid programId, CancellationToken ct)
    {
        if (owner.Header is not { } header || owner.ProgramId != programId)
            return new(owner, new SystemDescriptionRules(owner.TenantId, programId, owner.Id), []);
        if (owner.CurrentAuthorityContent is not { } authorityRef || owner.CurrentVersionContent is not { } currentRef ||
            authorityRef.Schema != SystemDescriptionAuthority.Schema || currentRef.Schema != SystemDescriptionContent.CurrentSchema)
            throw new ImmutableContentIntegrityException("The description lacks its exact native current authority.");
        var authority = await content.ReadJsonAsync<SystemDescriptionCurrentAuthority>(owner.TenantId.ToString(), authorityRef, ct).ConfigureAwait(false);
        var current = await content.ReadJsonAsync<SystemDescriptionVersionView>(owner.TenantId.ToString(), currentRef, ct).ConfigureAwait(false);
        if (authority.CurrentVersionContent != currentRef || !SystemDescriptionAuthority.MatchesCurrent(header, authority, current))
            throw new ImmutableContentIntegrityException("The description current authority differs from its compact native header and narrative.");
        if (owner.SourceSnapshotContent is { } proof)
            await new SystemDescriptionSnapshotSources(events, content).VerifyAsync(owner.TenantId, programId, current.Scope,
                current.Content.Sources, proof, ct).ConfigureAwait(false);
        var rules = SystemDescriptionRules.FromView(SystemDescriptionAuthority.CurrentView(header, authority, current));
        rules.SourceNativeSequence = owner.NativeSequence;
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new(owner, rules, []) { CurrentAuthority = authority };
    }
    internal async ValueTask<SystemDescriptionReadSnapshot> ReadAsync(SystemDescription owner, Uuid programId, CancellationToken ct)
    {
        var rules = new SystemDescriptionRules(owner.TenantId, programId, owner.Id);
        var facts = new List<DomainEvent>();
        if (owner.Header is null || owner.ProgramId != programId)
            return new(owner, rules, facts);
        var sequence = 0L;
        ISystemDescriptionFact? last = null;
        SystemDescriptionCurrentAuthority? authority = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(), SystemDescription.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not ISystemDescriptionFact fact || fact.NativeSequence != sequence + 1 ||
                record.Stream != owner.Stream || fact.TenantId != owner.TenantId || fact.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The description history has another native sequence or exact owner scope.");
            if (fact.NativeSequence > owner.NativeSequence)
                break;
            var mutation = await SystemDescriptionContent.ReadMutationAsync(content, record.Event, ct).ConfigureAwait(false);
            var current = await content.ReadJsonAsync<SystemDescriptionVersionView>(owner.TenantId.ToString(), fact.CurrentVersionContent, ct).ConfigureAwait(false);
            if (fact.CurrentVersionContent.Schema != SystemDescriptionContent.CurrentSchema)
                throw new ImmutableContentIntegrityException("The description current version has another immutable schema.");
            var before = rules.Read();
            var after = SystemDescriptionRules.FromView(before);
            if (fact.ChangeCount == 1)
                ApplyVerified(after, mutation);
            if (fact.CurrentAuthorityContent.Schema != SystemDescriptionAuthority.Schema)
                throw new ImmutableContentIntegrityException("The description decision authority has another immutable schema.");
            var nextAuthority = await content.ReadJsonAsync<SystemDescriptionCurrentAuthority>(owner.TenantId.ToString(), fact.CurrentAuthorityContent, ct).ConfigureAwait(false);
            var frozen = new FrozenSystemDescriptionMutation(before, after.Read(), fact.Header, fact.CurrentVersionContent,
                SystemDescriptionContent.Payload(fact), mutation, fact.Operation.CommandKind, fact.ChangeCount == 1)
            { BeforeAuthority = authority, Authority = nextAuthority, AuthorityContent = fact.CurrentAuthorityContent };
            if (!SystemDescriptionContent.Verify(frozen) || after.CurrentVersion is null ||
                AuthoredIntentDigest.Compute(current) != AuthoredIntentDigest.Compute(after.CurrentVersion))
                throw new ImmutableContentIntegrityException("The description source payload differs from its actual business transition and current authority.");
            if (fact.SourceSnapshotContent is { } snapshotProof)
                await new SystemDescriptionSnapshotSources(events, content).VerifyAsync(owner.TenantId, programId, current.Scope,
                    current.Content.Sources, snapshotProof, ct).ConfigureAwait(false);
            if (fact.ChangeCount == 1)
                ApplyVerified(rules, mutation);
            facts.Add(record.Event);
            last = fact;
            authority = nextAuthority;
            sequence = fact.NativeSequence;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The exact description native source cut is not readable yet.");
        if (last.Header != owner.Header || last.CurrentVersionContent != owner.CurrentVersionContent || last.CurrentAuthorityContent != owner.CurrentAuthorityContent)
            throw new ImmutableContentIntegrityException("The reconstructed description differs from its compact owner authority.");
        rules.ClearPendingChanges();
        rules.SourceNativeSequence = owner.NativeSequence;
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new(owner, rules, facts.AsReadOnly()) { CurrentAuthority = authority };
    }
    static void ApplyVerified(SystemDescriptionRules rules, object mutation)
    {
        try
        { rules.Apply(mutation); }
        catch (InvalidOperationException)
        { throw new ImmutableContentIntegrityException("The immutable description body violates its selected business transition."); }
    }
    public async ValueTask<SystemDescriptionVersionSource?> VersionSourceAsync(Uuid tenantId, Uuid programId, Uuid descriptionId,
        Uuid versionId, CancellationToken ct = default)
    {
        var snapshot = await ReadAsync(tenantId, programId, descriptionId, ct).ConfigureAwait(false);
        if (snapshot.FindVersion(versionId) is not { } version)
            return null;
        var producing = snapshot.Facts.FirstOrDefault(value => (value is SystemDescriptionDraftCreated or SystemDescriptionDraftRevised or
            SystemDescriptionSourcesBound or SystemDescriptionSuccessorProposed) && ((ISystemDescriptionFact)value).Header.CurrentVersionId == versionId);
        if (producing is null)
            throw new ImmutableContentIntegrityException("A description version has no exact native producing fact.");
        var current = snapshot.Facts.LastOrDefault(value => ((ISystemDescriptionFact)value).Header.CurrentVersionId == versionId &&
            ((ISystemDescriptionFact)value).Header.Status == version.Status) ?? producing;
        var reference = ((ISystemDescriptionFact)current).CurrentVersionContent;
        var exact = await content.ReadJsonAsync<SystemDescriptionVersionView>(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        if (exact.SystemDescriptionVersionId != versionId || exact.ContentSha256 != version.ContentSha256)
            throw new ImmutableContentIntegrityException("The description version proof differs from its exact immutable bytes.");
        var approval = snapshot.Facts.OfType<SystemDescriptionApproved>().FirstOrDefault(value => value.Header.CurrentVersionId == versionId && value.ChangeCount == 1);
        return new(exact, reference, NativeFactReference.Of(snapshot.Stream, producing),
            approval is null ? null : NativeFactReference.Of(snapshot.Stream, approval), snapshot.FindApproval(versionId),
            snapshot.Owner.NativeSequence, snapshot.Owner.CommittedStreamPosition);
    }
    public async ValueTask<SystemDescriptionVersionSource?> VersionSourceAsync(Uuid tenantId, Uuid programId, Uuid descriptionId,
        long version, CancellationToken ct = default)
    {
        var snapshot = await ReadAsync(tenantId, programId, descriptionId, ct).ConfigureAwait(false);
        return snapshot.FindVersion(version) is not { } selected ? null :
            await VersionSourceAsync(tenantId, programId, descriptionId, selected.SystemDescriptionVersionId, ct).ConfigureAwait(false);
    }
}
