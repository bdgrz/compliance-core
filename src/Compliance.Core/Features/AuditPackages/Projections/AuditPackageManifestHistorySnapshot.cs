using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Projections;

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
