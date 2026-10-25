using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>One complete immutable local decision selected by a bounded native header.</summary>
public interface IAuditPopulationStateFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    Uuid SubjectId { get; }
    AuditPopulationHeader Header { get; }
    ImmutableContentRef CurrentContent { get; }
}
