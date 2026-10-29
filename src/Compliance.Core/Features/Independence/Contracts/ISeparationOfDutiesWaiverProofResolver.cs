using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Independence.Contracts;

/// <summary>Produces and verifies immutable proofs of exact-scope separation-of-duties waivers.</summary>
public interface ISeparationOfDutiesWaiverProofResolver
{
    /// <summary>
    /// Builds the proof for exactly this scope at <paramref name="at"/>. A waiver that does not authorize it is
    /// <see cref="RequestErrorKind.Forbidden"/>; a malformed scope is <see cref="RequestErrorKind.Validation"/>.
    /// </summary>
    ValueTask<Result<SeparationOfDutiesWaiverProof>> ResolveAsync(SeparationOfDutiesWaiverProofScope scope,
        DateTimeOffset at, CancellationToken ct = default);

    /// <summary>
    /// Re-reads the waiver and its approval fact and succeeds only when every proof field reproduces the stored
    /// waiver and <paramref name="at"/> lies inside the half-open validity interval.
    /// </summary>
    ValueTask<Result> VerifyAsync(SeparationOfDutiesWaiverProof proof, DateTimeOffset at,
        CancellationToken ct = default);
}
