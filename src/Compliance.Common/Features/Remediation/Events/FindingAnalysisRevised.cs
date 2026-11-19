using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>An attributed analysis revision; the original finding source remains immutable.</summary>
[Discriminator("bdgrz.finding.analysis.revised", 1)]
public sealed record FindingAnalysisRevised(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, string Analysis, string Severity, IReadOnlyList<FindingLink> AffectedRecords,
    ActorReference RevisedBy, DateTimeOffset RevisedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
