using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Checks the active advisor assignment or guest grant for an exact handoff source.</summary>
public interface IAdvisorSharedSourceAuthorization
{
    ValueTask<bool> CanReadSelectedSourceAsync(Uuid tenantId, Uuid programId, Uuid handoffId,
        AdvisorRecordVersion sourceVersion, Uuid userId, CancellationToken ct = default);
}
