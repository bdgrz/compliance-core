using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>Read-only discovery of one admitted local selection; never mutation or disclosure authority.</summary>
public sealed record AuditorSampleSelectionLocator(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId,
    Uuid SampleSelectionId, DateTimeOffset AdmittedAt);
