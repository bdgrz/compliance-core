using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Keeps the current indexed entries for one Control draft without tenant-wide scans.</summary>
sealed record ControlDraftReferenceState(Uuid TenantId, Uuid ControlId, Uuid ProgramId,
    string Identifier, long Revision, IReadOnlyList<Uuid> EntryIds);
