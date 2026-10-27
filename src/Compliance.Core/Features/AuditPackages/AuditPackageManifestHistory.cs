using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Complete transient native manifest validation outside bounded command authority.</summary>
public static class AuditPackageManifestHistory
{
    public static async Task<AuditPackageManifestHistorySnapshot> ReadAsync(IDomainEventReader events,
        AuditPackageManifest owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var operations = new HashSet<(Uuid LogicalId, string Kind)>();
        var versions = new Dictionary<long, ImmutableContentRef>();
        var frozen = new HashSet<long>();
        var builds = new Dictionary<Uuid, AuditPackageBuildRequested>();
        var completed = new HashSet<Uuid>();
        var digest = AuditPackageReplayDigest.Empty;
        long revision = 0;
        long current = 0;
        var program = Uuid.Empty;
        foreach (var domainEvent in facts)
        {
            if (domainEvent is not IAuditPackageManifestFact fact || fact.Operation is not { } operation ||
                fact.TenantId != owner.TenantId || fact.SubjectId != owner.Id || fact.Revision != ++revision ||
                operation.LogicalId == Uuid.Empty || operation.RootOperationId == Uuid.Empty ||
                !operations.Add((operation.LogicalId, operation.CommandKind)) || !Own(owner, operation, fact.PackageVersion) ||
                fact.Change is null || fact.ActorMemberId != Member(operation) ||
                operation.AcceptedAt == default || fact.OccurredAt != operation.AcceptedAt ||
                operation.AuthoredIntentSha256.Length != 64 || !operation.AuthoredIntentSha256.All(Uri.IsHexDigit))
                throw Invalid("The native package history has another owner, revision, actor or repeated operation.");
            var kind = Kind(domainEvent);
            if (operation.CommandKind != kind || domainEvent is not AuditPackageBuilt &&
                !Root(owner, operation, kind, domainEvent is AuditPackageCreated ? null : revision - 1))
                throw Invalid("The native package history differs from its original command/revision authority.");
            if (fact.OperationResult is null && domainEvent is not (AuditPackageBuildRequested or AuditPackageDeliveryAdmitted))
                throw Invalid("A terminal native package fact must retain its original typed result envelope.");
            if (domainEvent is AuditPackageCreated)
            {
                if (revision != 1 || fact.PackageVersion != 1 || fact.ProgramId == Uuid.Empty || operation.LogicalId != owner.Id)
                    throw Invalid("A package must begin once at its exact first-owner identity.");
                program = fact.ProgramId;
                current = 1;
                versions.Add(current, fact.Change);
            }
            else
            {
                if (program == Uuid.Empty || fact.ProgramId != program)
                    throw Invalid("A package history cannot change its authoritative program binding.");
                if (domainEvent is AuditPackageAmended)
                {
                    if (fact.PackageVersion != current + 1)
                        throw Invalid("An amendment must introduce the next exact manifest version.");
                    current = fact.PackageVersion;
                    versions.Add(current, fact.Change);
                }
                else if (!versions.ContainsKey(fact.PackageVersion))
                    throw Invalid("A package fact names an uncreated manifest version.");
                if (domainEvent is AuditPackageDeliveryAdmitted delivery)
                {
                    if (delivery.Owner != owner.Stream || delivery.Generation != fact.PackageVersion || fact.PackageVersion != current)
                        throw Invalid("Delivery must freeze the exact current manifest generation.");
                    frozen.Add(fact.PackageVersion);
                }
                else if (domainEvent is not AuditPackageAmended && frozen.Contains(fact.PackageVersion))
                    throw Invalid("A delivery-admitted manifest version cannot change, including unselected historical versions.");
            }
            if (domainEvent is AuditPackageBuildRequested requested)
            {
                if (requested.PackageVersion != current || !builds.TryAdd(operation.RootOperationId, requested))
                    throw Invalid("A build admission must identify one current unfrozen manifest root.");
            }
            if (domainEvent is AuditPackageBuilt built)
            {
                if (!builds.TryGetValue(operation.RootOperationId, out var admission) ||
                    built.PackageVersion != admission.PackageVersion || operation.CausationEventId != admission.Metadata.EventId ||
                    operation.Actor != admission.Operation.Actor || operation.Episode != admission.Operation.Episode ||
                    operation.OriginalExpectedRevision != admission.Operation.OriginalExpectedRevision ||
                    operation.AuthoredIntentSha256 != admission.Operation.AuthoredIntentSha256 ||
                    (operation.FirstOwner ?? owner.Stream) != (admission.Operation.FirstOwner ?? owner.Stream) ||
                    !completed.Add(operation.RootOperationId))
                    throw Invalid("A successful build must follow its exact original admitted root once.");
            }
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedCommandKind is null || operation.CommandKind == owner.Acceptance.SelectedCommandKind) &&
                (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(domainEvent) || selected.Metadata != domainEvent.Metadata))
                throw Invalid("The selected native package acceptance differs from its actual full metadata and payload.");
            digest = AuditPackageReplayDigest.Append(digest, domainEvent);
        }
        if (revision != owner.Revision || current != owner.CurrentVersion || program != owner.ProgramId ||
            (current > 0 && (owner.CurrentContent != versions[current] || owner.IsFrozen(current) != frozen.Contains(current))) ||
            owner.SelectedVersion > 0 && versions.TryGetValue(owner.SelectedVersion, out var selectedVersion) &&
            (owner.SelectedContent != selectedVersion || owner.IsFrozen(owner.SelectedVersion) != frozen.Contains(owner.SelectedVersion)))
            throw Invalid("The composed manifest authority differs from its exact hydrated source prefix.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new AuditPackageManifestHistorySnapshot(owner, facts, versions, frozen);
    }

    static bool Own(AuditPackageManifest owner, OperationProvenance operation, long version) => version > 0 &&
        operation.Owner == owner.Stream && operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream &&
        operation.Episode == version.ToString(CultureInfo.InvariantCulture);
    static bool Root(AuditPackageManifest owner, OperationProvenance operation, string kind, long? revision) =>
        operation.CommandKind == kind && operation.LogicalId == operation.RootOperationId && operation.CausationEventId is null &&
        (operation.FirstOwner ?? owner.Stream) == owner.Stream && operation.OriginalExpectedRevision ==
        (revision is { } expected ? (ulong)expected : null);
    static Uuid? Member(OperationProvenance operation) => operation.Actor.Kind == "member" &&
        Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var value) ? value : null;
    static string Kind(DomainEvent fact) => fact switch
    {
        AuditPackageCreated => nameof(CreateAuditPackage),
        AuditPackageAmended => nameof(AmendAuditPackage),
        AuditPackageBuildRequested => nameof(RequestAuditPackageBuild),
        AuditPackageBuilt => nameof(RecordAuditPackageBuildResult),
        AuditPackageValidated => nameof(ValidateAuditPackage),
        AuditPackageValidationOverrideApproved => nameof(ApproveAuditPackageValidationOverride),
        AuditPackageDisclosureApproved => nameof(ApproveAuditPackageDisclosure),
        AuditPackageDeliveryAdmitted => nameof(RecordAuditPackageDelivery),
        _ => throw Invalid("The package history contains another native fact kind."),
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
