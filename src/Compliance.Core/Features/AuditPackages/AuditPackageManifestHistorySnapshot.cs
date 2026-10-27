using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Verified read/preparation history; collections are never stored in Aggregate command state.</summary>
public sealed class AuditPackageManifestHistorySnapshot
{
    internal AuditPackageManifestHistorySnapshot(AuditPackageManifest owner, IReadOnlyList<DomainEvent> facts,
        IReadOnlyDictionary<long, ImmutableContentRef> versions, IReadOnlySet<long> frozen)
    {
        Owner = owner;
        Facts = Array.AsReadOnly(facts.ToArray());
        Cut = AuditPackagePreparedCut.Capture(owner);
        _versions = versions;
        _frozen = frozen;
    }
    readonly IReadOnlyDictionary<long, ImmutableContentRef> _versions;
    readonly IReadOnlySet<long> _frozen;
    internal AuditPackageManifest Owner { get; }
    internal AuditPackagePreparedCut Cut { get; }
    public IReadOnlyList<DomainEvent> Facts { get; }
    public ImmutableContentRef? VersionContent(long version) => _versions.GetValueOrDefault(version);
    public bool IsFrozen(long version) => _frozen.Contains(version);
    public DomainEvent? FactById(Uuid eventId) => Facts.FirstOrDefault(fact => fact.Metadata.EventId == eventId);
}
