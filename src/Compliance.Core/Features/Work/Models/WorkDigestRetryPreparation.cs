using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>One exact finalized physical/native and selected-week cut, before any immutable candidate writes.</summary>
sealed record WorkDigestRetryPreparation(ulong Revision, ulong PhysicalPosition,
    WorkDigestDispatchStatusView Before, DateTimeOffset RetryDeadline, WorkDigestDispatchStatusView Result);
