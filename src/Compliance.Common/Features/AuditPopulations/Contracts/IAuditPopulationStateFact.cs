using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Contracts;

/// <summary>One complete immutable local decision selected by a bounded native header.</summary>
public interface IAuditPopulationStateFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    Uuid SubjectId { get; }
    AuditPopulationHeader Header { get; }
    ImmutableContentRef CurrentContent { get; }
}
