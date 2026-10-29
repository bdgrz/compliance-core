using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Risks.Models;

/// <summary>
///     The authored completion review frozen at admission: the program and risk of the action, the outcome, the
///     normalized rationale, the reviewer and, when the reviewer needed one, the waiver identity with the exact
///     separation-of-duties waiver proof that authorizes it. The decision id is the root operation id.
/// </summary>
public sealed record RiskActionReviewDecision(Uuid DecisionId, Uuid ProgramId, Uuid RiskId, string Outcome,
    string Rationale, Uuid ReviewerMemberId, ActorReference Reviewer, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId, ImmutableContentRef? WaiverProof);
