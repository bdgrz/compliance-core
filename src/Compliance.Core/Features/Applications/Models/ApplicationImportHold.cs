using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Applications.Events;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
///     What one declared application recorded for one import batch: the exclusive hold, the fence that blocks a
///     delayed operation, the provisional effects of the batch's frozen group and the settlement. A fence may exist
///     without a hold. The record is rebuilt from the application's ordered stream alone.
/// </summary>
public sealed record ApplicationImportHold(Uuid BatchId, ApplicationImportTargetAuthorized? Reserved,
    ApplicationImportEffectCanceled? Fence, IReadOnlyList<ApplicationImportEffectPending> Effects,
    ApplicationImportTargetSettled? Settled)
{
    /// <summary>True while the batch holds the application and has not settled.</summary>
    public bool IsActive => Reserved is not null && Settled is null;

    /// <summary>True when the batch's provisional effects were appended.</summary>
    public bool IsApplied => Effects.Count > 0;
}
