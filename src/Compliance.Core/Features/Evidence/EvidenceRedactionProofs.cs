using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Freezes and verifies actual scoped artifact, preparation and waiver records; never grants a source hold.</summary>
sealed class EvidenceRedactionProofs(IDomainEventReader events, ISourceFactResolver resolver, IImmutableContentStore content)
{
    public async Task<ImmutableContentRef> FreezeAsync(EvidenceRedaction owner, EvidenceRedactionSourcePair sources,
        SeparationOfDutiesWaiver? waiver, bool approval, CancellationToken ct)
    {
        var original = await ReadArtifactAsync(sources.Original.Metadata.TenantId, sources.Original.Metadata.ArtifactId,
            sources.Original.Metadata.SourcePosition, ct).ConfigureAwait(false);
        var derived = await ReadArtifactAsync(sources.Derived.Metadata.TenantId, sources.Derived.Metadata.ArtifactId,
            sources.Derived.Metadata.SourcePosition, ct).ConfigureAwait(false);
        if (original.State != sources.Original.Metadata.State || derived.State != sources.Derived.Metadata.State ||
            original.Registration.Event.Metadata.EventId != sources.Original.RegistrationEventId ||
            derived.Registration.Event.Metadata.EventId != sources.Derived.RegistrationEventId ||
            original.AvailableAt != sources.Original.AvailabilityRecordedAt || derived.AvailableAt != sources.Derived.AvailabilityRecordedAt)
            throw Invalid("The source cut changed after canonical artifact qualification.");
        var originalProof = await BuildAsync<EvidenceArtifactRegistered>(original.Registration, ct).ConfigureAwait(false);
        var derivedProof = await BuildAsync<EvidenceArtifactRegistered>(derived.Registration, ct).ConfigureAwait(false);
        SourceFactProof<EvidenceArtifactInspected>? inspected = null;
        SourceFactProof<EvidenceArtifactQuarantineReleased>? released = null;
        if (derived.Availability is { Event: EvidenceArtifactInspected } inspection)
            inspected = await BuildAsync<EvidenceArtifactInspected>(inspection, ct).ConfigureAwait(false);
        else if (derived.Availability is { Event: EvidenceArtifactQuarantineReleased } release)
            released = await BuildAsync<EvidenceArtifactQuarantineReleased>(release, ct).ConfigureAwait(false);
        SourceFactProof<EvidenceRedactionPrepared>? legacy = null;
        SourceFactProof<EvidenceRedactionPreparedV2>? native = null;
        if (approval)
        {
            var current = owner.CurrentPreparationFact ?? throw Invalid("An approval has no actual preparation source.");
            var record = (await ReadRawAsync(owner.Stream, owner.CommittedStreamPosition, ct).ConfigureAwait(false)).Records
                .SingleOrDefault(item => item.Event.Metadata.EventId == current.Metadata.EventId)
                ?? throw new ImmutableContentUnavailableException("The actual preparation source is not visible yet.");
            if (record.Event.GetType() != current.GetType() || !record.Event.Equals(current) || record.Event.Metadata != current.Metadata)
                throw Invalid("The actual preparation source differs from the prepared current owner cut.");
            if (current is EvidenceRedactionPrepared)
                legacy = await BuildAsync<EvidenceRedactionPrepared>(record, ct).ConfigureAwait(false);
            else if (current is EvidenceRedactionPreparedV2)
                native = await BuildAsync<EvidenceRedactionPreparedV2>(record, ct).ConfigureAwait(false);
            else
                throw Invalid("The preparation source has an unsupported version.");
        }
        SourceFactProof<SeparationOfDutiesWaiverRecorded>? recorded = null;
        SourceFactProof<SeparationOfDutiesWaiverApproved>? approved = null;
        string? waiverDigest = null;
        if (waiver is not null)
        {
            var prefix = await ReadRawAsync(waiver.Stream, waiver.CommittedStreamPosition, ct).ConfigureAwait(false);
            var recordings = prefix.Records.Where(record => record.Event is SeparationOfDutiesWaiverRecorded).ToArray();
            var approvals = prefix.Records.Where(record => record.Event is SeparationOfDutiesWaiverApproved).ToArray();
            if (recordings.Length != 1 || approvals.Length != 1)
                throw Invalid("The waiver must retain one exact recording and independent approval source.");
            var recording = recordings[0];
            var approvalRecord = approvals[0];
            recorded = await BuildAsync<SeparationOfDutiesWaiverRecorded>(recording, ct).ConfigureAwait(false);
            approved = await BuildAsync<SeparationOfDutiesWaiverApproved>(approvalRecord, ct).ConfigureAwait(false);
            waiverDigest = prefix.Digest;
        }
        var manifest = new EvidenceRedactionNativeSources(originalProof, sources.Original.Metadata.SourcePosition,
            original.State, original.Digest, derivedProof, sources.Derived.Metadata.SourcePosition, derived.State,
            derived.Digest, inspected, released, legacy, native, recorded, approved,
            waiver?.CommittedStreamPosition, waiverDigest);
        return await NativeContent.WriteAsync(content, owner.TenantId, EvidenceRedactionNativeSources.Schema, manifest, ct)
            .ConfigureAwait(false);
    }

    public async Task VerifyAsync(EvidenceRedaction owner, ImmutableContentRef reference,
        EvidenceRedactionPreparationFact prepared, EvidenceRedactionApprovalFact? approval, DomainEvent? preparationSource, CancellationToken ct)
    {
        if (reference is null || reference.Schema != EvidenceRedactionNativeSources.Schema)
            throw Invalid("The native redaction fact has another source-proof schema.");
        var sources = await NativeContent.ReadAsync<EvidenceRedactionNativeSources>(content, owner.TenantId, reference, ct)
            .ConfigureAwait(false);
        if (sources.OriginalRegistration is null || sources.DerivedRegistration is null)
            throw Invalid("The retained source manifest is missing an exact registration proof.");
        var original = await VerifyRegistrationAsync(sources.OriginalRegistration, prepared.Original, ct).ConfigureAwait(false);
        var derived = await VerifyRegistrationAsync(sources.DerivedRegistration, prepared.Derived, ct).ConfigureAwait(false);
        var originalCut = await ReadArtifactAsync(owner.TenantId, prepared.Original.ArtifactId, sources.OriginalPhysicalPosition, ct)
            .ConfigureAwait(false);
        var derivedCut = await ReadArtifactAsync(owner.TenantId, prepared.Derived.ArtifactId, sources.DerivedPhysicalPosition, ct)
            .ConfigureAwait(false);
        if (originalCut.State != sources.OriginalState || derivedCut.State != sources.DerivedState ||
            originalCut.Digest != sources.OriginalPrefixSha256 || derivedCut.Digest != sources.DerivedPrefixSha256 ||
            originalCut.Registration.Event.Metadata.EventId != original.EventId || derivedCut.Registration.Event.Metadata.EventId != derived.EventId)
            throw Invalid("The actual source prefixes differ from the native redaction proof.");
        if (sources.Inspection is { } inspection)
        {
            var source = await VerifySourceAsync(inspection, ct).ConfigureAwait(false);
            if (source.Source != EvidenceStreams.Address(owner.TenantId, prepared.Derived.ArtifactId) ||
                source.Facts.State != EvidenceArtifactStates.Available || source.Facts.Reason is not null ||
                derivedCut.Availability?.Event.Metadata.EventId != source.EventId || sources.Release is not null)
                throw Invalid("The retained inspection proof is not the exact clean availability source.");
        }
        else if (sources.Release is { } release)
        {
            var source = await VerifySourceAsync(release, ct).ConfigureAwait(false);
            if (source.Source != EvidenceStreams.Address(owner.TenantId, prepared.Derived.ArtifactId) ||
                source.Facts.RescanState != "clean" || derivedCut.Availability?.Event.Metadata.EventId != source.EventId)
                throw Invalid("The retained release proof is not the exact available derivative source.");
        }
        else if (derivedCut.Availability is not null)
            throw Invalid("An available derivative is missing its actual availability source proof.");
        if (approval is null)
        {
            if (sources.LegacyPreparation is not null || sources.NativePreparation is not null || sources.WaiverRecorded is not null ||
                sources.WaiverApproved is not null || sources.WaiverPhysicalPosition is not null || sources.WaiverPrefixSha256 is not null)
                throw Invalid("A preparation cannot manufacture an approval or waiver source.");
            return;
        }
        if (derivedCut.State != EvidenceArtifactStates.Available || approval.DerivedSourcePosition != sources.DerivedPhysicalPosition ||
            approval.DerivedAvailableAt != derivedCut.AvailableAt)
            throw Invalid("The approval differs from its exact observed available derivative cut.");
        if (sources.LegacyPreparation is { } legacy && sources.NativePreparation is null)
        {
            var source = await VerifySourceAsync(legacy, ct).ConfigureAwait(false);
            if (preparationSource is not EvidenceRedactionPrepared actual || source.Source != owner.Stream ||
                source.EventId != actual.Metadata.EventId || source.OwnerRevision != actual.Metadata.AggregateVersion ||
                source.Facts != actual || source.Facts.Metadata != actual.Metadata || source.Facts.Preparation != prepared)
                throw Invalid("The approval names another exact legacy preparation source.");
        }
        else if (sources.NativePreparation is { } native && sources.LegacyPreparation is null)
        {
            var source = await VerifySourceAsync(native, ct).ConfigureAwait(false);
            if (preparationSource is not EvidenceRedactionPreparedV2 actual || source.Source != owner.Stream ||
                source.EventId != actual.Metadata.EventId || source.OwnerRevision != actual.Metadata.AggregateVersion ||
                source.Facts != actual || source.Facts.Metadata != actual.Metadata || source.Facts.Preparation != prepared)
                throw Invalid("The approval names another exact native preparation source.");
        }
        else
            throw Invalid("An approval requires one exact preparation source version.");
        await VerifyWaiverAsync(owner, sources, approval, ct).ConfigureAwait(false);
    }

    async Task VerifyWaiverAsync(EvidenceRedaction owner, EvidenceRedactionNativeSources sources,
        EvidenceRedactionApprovalFact approval, CancellationToken ct)
    {
        if (approval.SeparationOfDutiesWaiver is not { } waiver)
        {
            if (sources.WaiverRecorded is not null || sources.WaiverApproved is not null || sources.WaiverPhysicalPosition is not null ||
                sources.WaiverPrefixSha256 is not null)
                throw Invalid("An unwaived decision carries unrelated waiver provenance.");
            return;
        }
        if (sources.WaiverRecorded is not { } recording || sources.WaiverApproved is not { } approving ||
            sources.WaiverPhysicalPosition is not { } position || sources.WaiverPrefixSha256 is not { } digest)
            throw Invalid("The exact waiver recording and approval proofs are missing.");
        var recorded = await VerifySourceAsync(recording, ct).ConfigureAwait(false);
        var approved = await VerifySourceAsync(approving, ct).ConfigureAwait(false);
        var expectedStream = new SeparationOfDutiesWaiver(owner.TenantId, waiver.WaiverId).Stream;
        var prefix = await ReadRawAsync(expectedStream, position, ct).ConfigureAwait(false);
        if (prefix.Digest != digest || recorded.Source != expectedStream || approved.Source != expectedStream ||
            recorded.Facts.TenantId != owner.TenantId || approved.Facts.TenantId != owner.TenantId ||
            recorded.Facts.WaiverId != waiver.WaiverId || approved.Facts.WaiverId != waiver.WaiverId ||
            recorded.Facts.Scope != waiver.Scope || recorded.Facts.BeneficiaryMemberId != waiver.BeneficiaryMemberId ||
            recorded.Facts.RequesterMemberId != waiver.RequesterMemberId || approved.Facts.ApproverMemberId != waiver.ApproverMemberId ||
            recorded.Facts.RequestedAt != waiver.RequestedAt || approved.Facts.ApprovedAt != waiver.ApprovedAt ||
            recorded.Facts.ExpiresAt != waiver.ExpiresAt || recorded.PhysicalPosition >= approved.PhysicalPosition ||
            approved.PhysicalPosition >= position || prefix.Records.Count(record => !record.Event.Metadata.IsAudit) != 2)
            throw Invalid("The bounded waiver value differs from its actual independently approved source cut.");
    }

    async Task<VerifiedSourceProof<EvidenceArtifactRegistered>> VerifyRegistrationAsync(SourceFactProof<EvidenceArtifactRegistered> proof,
        EvidenceRedactionSourceCapsule capsule, CancellationToken ct)
    {
        var source = await VerifySourceAsync(proof, ct).ConfigureAwait(false);
        var registered = source.Facts;
        var actual = new EvidenceRedactionSourceCapsule(registered.TenantId, registered.ArtifactId, source.EventId,
            source.PayloadSha256.ToLowerInvariant(), registered.ContentSha256, registered.ContentLength, registered.RegisteredAt, "");
        actual = actual with { IdentitySha256 = EvidenceRedaction.IdentityDigest(actual) };
        if (source.Source != EvidenceStreams.Address(capsule.TenantId, capsule.ArtifactId) || actual != capsule)
            throw Invalid("The exact registration proof differs from its retained typed source capsule.");
        return source;
    }

    async Task<SourceFactProof<T>> BuildAsync<T>(DomainEventRecord record, CancellationToken ct) where T : DomainEvent
    {
        try
        {
            return (await SourceFactProofs.BuildAsync<T>(resolver, content, record.Stream.Realm,
                NativeFactReference.Of(record.Stream, record.Event), record.ResourceOffset, ct).ConfigureAwait(false)).Proof;
        }
        catch (SourceFactUnavailableException error)
        {
            throw new ImmutableContentUnavailableException(error.Message);
        }
        catch (SourceFactVerificationException error)
        {
            throw Invalid(error.Message);
        }
    }

    async Task<VerifiedSourceProof<T>> VerifySourceAsync<T>(SourceFactProof<T> proof, CancellationToken ct) where T : DomainEvent
    {
        try
        {
            if (proof.Source is null || proof.EventId == Uuid.Empty || proof.OwnerRevision == 0 ||
                proof.RequiredFacts is null || proof.RequiredFacts.Schema != SourceFactProofs.RequiredFactsSchema)
                throw Invalid("The retained source proof is missing its exact native identity and locator content.");
            var verified = await resolver.VerifyAsync(proof, ct).ConfigureAwait(false);
            var locator = await content.ReadJsonAsync<SourceFactLocator>(proof.Source.Realm, proof.RequiredFacts, ct).ConfigureAwait(false);
            if (locator.Source != verified.Source || locator.EventId != verified.EventId || locator.ResourcePosition != verified.PhysicalPosition ||
                locator.AggregateVersion != verified.OwnerRevision || locator.GlobalPosition != verified.GlobalPosition ||
                locator.RealmPosition != verified.RealmPosition || !string.Equals(locator.PayloadSha256, verified.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                throw Invalid("The retained locator content differs from the actual verified source tuple.");
            return verified;
        }
        catch (SourceFactUnavailableException error)
        {
            throw new ImmutableContentUnavailableException(error.Message);
        }
        catch (SourceFactVerificationException error)
        {
            throw Invalid(error.Message);
        }
    }

    async Task<RawPrefix> ReadRawAsync(EventStreamAddress stream, ulong through, CancellationToken ct)
    {
        if (through == 0 || !Uuid.TryParse(stream.Resource, CultureInfo.InvariantCulture, out var id))
            throw Invalid("An observed source cut requires an exact owner identity and committed position.");
        var records = new List<DomainEventRecord>();
        var ids = new HashSet<Uuid>();
        var digest = EvidenceRedactionReplayCut.Empty;
        ulong position = 0;
        ulong version = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (position == through)
                break;
            var metadata = record.Event.Metadata;
            if (record.Stream != stream || record.ResourceOffset != position || metadata.AggregateId != id ||
                metadata.EventId == Uuid.Empty || !ids.Add(metadata.EventId) ||
                !metadata.IsAudit && metadata.AggregateVersion != ++version)
                throw Invalid("The exact source cut has another physical/native prefix or metadata identity.");
            records.Add(record);
            digest = EvidenceRedactionReplayCut.Append(digest, record.Event, true);
            position++;
        }
        if (position != through)
            throw new ImmutableContentUnavailableException("The exact observed source prefix is not visible yet.");
        return new RawPrefix(records, digest);
    }

    async Task<ArtifactPrefix> ReadArtifactAsync(Uuid tenantId, Uuid artifactId, ulong through, CancellationToken ct)
    {
        var raw = await ReadRawAsync(EvidenceStreams.Address(tenantId, artifactId), through, ct).ConfigureAwait(false);
        DomainEventRecord? registration = null;
        DomainEventRecord? availability = null;
        string? state = null;
        string? reason = null;
        DateTimeOffset? at = null;
        long revision = 0;
        foreach (var record in raw.Records)
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            revision++;
            switch (record.Event)
            {
                case EvidenceArtifactRegistered value when registration is null && value.TenantId == tenantId &&
                    value.ArtifactId == artifactId && value.Collector is not null &&
                    value.Content is { Title: not null, EvidenceType: not null, Source: not null, HandlingClass: not null } &&
                    EvidenceRules.Validate(value.Content, value.ContentSha256, value.ContentLength) is null:
                    registration = record;
                    state = EvidenceArtifactStates.PendingInspection;
                    break;
                case EvidenceArtifactInspected value when registration is not null && value.TenantId == tenantId &&
                    value.ArtifactId == artifactId && state == EvidenceArtifactStates.PendingInspection &&
                    (value.State == EvidenceArtifactStates.Available && value.Reason is null ||
                     value.State == EvidenceArtifactStates.Quarantined && value.Reason == EvidenceArtifactStates.Malware ||
                     value.State == EvidenceArtifactStates.Rejected && value.Reason is EvidenceArtifactStates.SecretDetected or EvidenceArtifactStates.Invalid):
                    state = value.State;
                    reason = value.Reason;
                    if (state == EvidenceArtifactStates.Available)
                        (availability, at) = (record, value.InspectedAt);
                    break;
                case EvidenceArtifactQuarantineReleased value when value.TenantId == tenantId && value.ArtifactId == artifactId &&
                    state == EvidenceArtifactStates.Quarantined && reason == EvidenceArtifactStates.Malware && value.Revision == revision &&
                    value.RequestId != Uuid.Empty && value.RescanState == "clean" && value.RescannedAt != default &&
                    value.RescannedAt <= value.DecidedAt && value.DecidedAt != default && !string.IsNullOrWhiteSpace(value.Rationale) &&
                    value.Rationale.Length <= EvidenceRules.MaximumTextLength && value.DecidedBy is { Kind: "member", Id.Length: > 0, Display.Length: > 0 }:
                    state = EvidenceArtifactStates.Available;
                    reason = null;
                    (availability, at) = (record, value.DecidedAt);
                    break;
                case EvidenceArtifactDownloadIssued value when registration?.Event is EvidenceArtifactRegistered registered &&
                    value.TenantId == tenantId && value.ArtifactId == artifactId && state == EvidenceArtifactStates.Available && reason is null &&
                    value.Revision == revision && value.RequestId != Uuid.Empty && value.ContentSha256 == registered.ContentSha256 &&
                    value.ContentLength == registered.ContentLength && !string.IsNullOrWhiteSpace(value.Purpose) &&
                    value.Purpose == value.Purpose.Trim() && value.Purpose.Length <= EvidenceRules.MaximumTextLength &&
                    value.IssuedBy is { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } && value.IssuedAt >= registered.RegisteredAt && value.IssuedAt != default:
                    break;
                default:
                    throw Invalid("The retained artifact source cut has an impossible canonical lifecycle.");
            }
        }
        return registration is not null && state is not null
            ? new ArtifactPrefix(registration, availability, state, at, raw.Digest)
            : throw Invalid("The retained source cut has no exact canonical registration.");
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
    sealed record RawPrefix(IReadOnlyList<DomainEventRecord> Records, string Digest);
    sealed record ArtifactPrefix(DomainEventRecord Registration, DomainEventRecord? Availability, string State,
        DateTimeOffset? AvailableAt, string Digest);
}
