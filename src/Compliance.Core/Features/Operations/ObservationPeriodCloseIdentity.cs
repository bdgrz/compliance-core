using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Read-only scoped locator; the same close id on different periods is retained as an explicit ambiguity.</summary>
public sealed record ObservationPeriodCloseIdentity(Uuid TenantId, Uuid ProgramId, Uuid CloseId, Uuid PeriodId,
    int Version, long PeriodRevision, string SnapshotSha256, NativeFactReference Source);
