using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
///     The slot of one batch on its source: the admission fact and the settlement fact once it exists. A settlement
///     with no admission is a fence.
/// </summary>
public sealed record ApplicationImportSourceSlot(Uuid BatchId, ApplicationImportSourceAdmissionRecorded? Admitted,
    ApplicationImportSourceSettled? Settled)
{
    /// <summary>True while the batch holds the source.</summary>
    public bool IsActive => Admitted is not null && Settled is null;

    /// <summary>True when the batch was settled before it was ever admitted.</summary>
    public bool IsFence => Admitted is null && Settled is not null;
}
