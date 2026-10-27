using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The exact admitted decision and frozen gap that authoritatively failed; incompatible unbound v1 is refused.</summary>
public sealed record ReadinessDecisionFailureProof(Uuid ProgramId, Uuid AssessmentId, string Reason, Uuid? GapId,
    NativeFactReference Admission, Uuid OperationId, long Generation, ImmutableContentRef Intent);
