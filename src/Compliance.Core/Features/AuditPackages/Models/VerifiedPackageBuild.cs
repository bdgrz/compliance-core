using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Projections;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Verified immutable build admission and exact manifest/selection, independent of current source owners.</summary>
public sealed record VerifiedPackageBuild(AuditPackageManifest Owner, AuditPackageBuildRequested Admission,
    PackageBuildIntent Intent, AuditPackageManifestView Manifest, AuditPackageManifestHistorySnapshot History);
