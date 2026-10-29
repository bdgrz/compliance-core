using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryAffectedRecord(Uuid TenantId, string Context,
    string RecordType, Uuid RecordId, string Reason);
