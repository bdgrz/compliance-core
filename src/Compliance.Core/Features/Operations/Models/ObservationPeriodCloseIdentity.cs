using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Read-only scoped locator; the same close id on different periods is retained as an explicit ambiguity.</summary>
public sealed record ObservationPeriodCloseIdentity(Uuid TenantId, Uuid ProgramId, Uuid CloseId, Uuid PeriodId,
    int Version, long PeriodRevision, string SnapshotSha256, NativeFactReference Source);
