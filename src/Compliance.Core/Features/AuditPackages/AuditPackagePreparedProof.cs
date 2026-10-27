using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Unpublished proof selection tied to a verified complete local source cut; not a durable/public authority.</summary>
sealed class AuditPackagePreparedProof
{
    readonly EventStreamAddress _owner;
    readonly ulong _version;
    readonly ulong _position;
    readonly string _digest;
    readonly AuditPackageBuildRequested? _buildAdmission;
    readonly NativeFactReference _build;
    readonly NativeFactReference? _disclosure;
    readonly IReadOnlyList<NativeFactReference> _validations;
    readonly long _manifestVersion;

    AuditPackagePreparedProof(AuditPackageManifestHistorySnapshot history, long manifestVersion,
        NativeFactReference build, AuditPackageBuildRequested? buildAdmission, NativeFactReference? disclosure,
        IReadOnlyList<NativeFactReference> validations)
    {
        (_owner, _version, _position, _digest) = (history.Cut.Owner, history.Cut.Version, history.Cut.Position, history.Cut.Digest);
        (_manifestVersion, _build, _buildAdmission, _disclosure, _validations) =
            (manifestVersion, build, buildAdmission, disclosure, validations.ToArray());
    }

    internal static Result<AuditPackagePreparedProof> ForBuild(AuditPackageManifestHistorySnapshot history,
        NativeFactReference source, long version, Uuid root)
    {
        if (source.Stream != history.Owner.Stream || history.FactById(source.EventId) is not AuditPackageBuildRequested admission ||
            admission.Metadata.AggregateVersion != source.AggregateVersion || admission.PackageVersion != version ||
            admission.Operation.RootOperationId != root || history.Facts.OfType<AuditPackageBuilt>().Any(fact => fact.Operation.RootOperationId == root))
            return Fail("The successful archive does not follow one exact uncompleted admitted build.");
        return Result<AuditPackagePreparedProof>.Success(new(history, version, source, admission, null, []));
    }

    internal static Result<AuditPackagePreparedProof> ForDelivery(AuditPackageManifestHistorySnapshot history,
        long version, NativeFactReference build, NativeFactReference disclosure, IReadOnlyList<NativeFactReference> validations)
    {
        if (!Contains<AuditPackageBuilt>(history, build, version) || !Contains<AuditPackageDisclosureApproved>(history, disclosure, version) ||
            validations.Count == 0 || validations.Any(proof => !Contains<AuditPackageValidated>(history, proof, version) &&
                !Contains<AuditPackageValidationOverrideApproved>(history, proof, version)))
            return Fail("Delivery requires this exact version's built archive, disclosure and complete native validation proofs.");
        var latest = history.Facts.OfType<AuditPackageValidated>().LastOrDefault(fact => fact.PackageVersion == version);
        if (latest is null || validations.All(proof => proof.EventId != latest.Metadata.EventId))
            return Fail("Delivery must pin the latest applicable native validation at the prepared source cut.");
        return Result<AuditPackagePreparedProof>.Success(new(history, version, build, null, disclosure, validations));
    }

    public bool MatchesBuild(AuditPackageManifest owner, OperationProvenance child, NativeFactReference source, long version) =>
        MatchesCut(owner) && _manifestVersion == version && _build == source && _buildAdmission is { } admission &&
        admission.Operation.RootOperationId == child.RootOperationId && admission.Operation.Actor == child.Actor &&
        admission.Operation.Episode == child.Episode && admission.Operation.OriginalExpectedRevision == child.OriginalExpectedRevision &&
        admission.Operation.AuthoredIntentSha256 == child.AuthoredIntentSha256 &&
        (admission.Operation.FirstOwner ?? owner.Stream) == (child.FirstOwner ?? owner.Stream);

    public bool MatchesDelivery(AuditPackageManifest owner, long version, NativeFactReference build,
        NativeFactReference disclosure, IReadOnlyList<NativeFactReference> validations) => MatchesCut(owner) &&
        _manifestVersion == version && _buildAdmission is null && _build == build && _disclosure == disclosure &&
        _validations.SequenceEqual(validations);

    internal bool MatchesCut(AuditPackageManifest owner) => owner.Stream == _owner && owner.Version == _version &&
        owner.CommittedStreamPosition == _position && owner.ReplayCut.Digest == _digest;

    static bool Contains<T>(AuditPackageManifestHistorySnapshot history, NativeFactReference proof, long version)
        where T : DomainEvent, IAuditPackageManifestFact => proof.Stream == history.Owner.Stream &&
        history.FactById(proof.EventId) is T fact && fact.PackageVersion == version &&
        fact.Metadata.AggregateVersion == proof.AggregateVersion;
    static Result<AuditPackagePreparedProof> Fail(string message) => Result<AuditPackagePreparedProof>.Failure(
        new RequestError(RequestErrorKind.Conflict, message));
}
