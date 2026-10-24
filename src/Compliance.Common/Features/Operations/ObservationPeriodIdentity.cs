using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Immutable discovery identity of a period after its opening is applied; this row is not current lifecycle state.</summary>
public sealed record ObservationPeriodIdentity(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId, DateTimeOffset OpenedAt);
