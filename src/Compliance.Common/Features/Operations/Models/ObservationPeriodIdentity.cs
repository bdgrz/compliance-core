using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Immutable discovery identity of a period after its opening is applied; this row is not current lifecycle state.</summary>
public sealed record ObservationPeriodIdentity(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId, DateTimeOffset OpenedAt);
