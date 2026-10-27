using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Incidents.Contracts;

public interface IComplianceIncidentFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    long Revision { get; }
    Uuid? ActorMemberId { get; }
    ComplianceIncidentAuthorityHeader Header { get; }
    ImmutableContentRef CurrentContent { get; }
    ImmutableContentRef Change { get; }
    long NativeSequence { get; }
    int ChangeCount { get; }
    ImmutableContentRef OperationResult { get; }
    DateTimeOffset OccurredAt { get; }
}
