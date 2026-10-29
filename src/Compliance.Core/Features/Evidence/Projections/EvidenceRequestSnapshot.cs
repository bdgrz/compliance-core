using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Projections;

/// <summary>A captured read-side inventory of one program's evidence requests; this is never retained as command state.</summary>
public sealed class EvidenceRequestSnapshot(IReadOnlyList<EvidenceRequestView> requests)
{
    /// <summary>Every request of the program in the order opened.</summary>
    public IReadOnlyList<EvidenceRequestView> Requests { get; } = requests;

    /// <summary>The request, or null when the program has no such request.</summary>
    public EvidenceRequestView? Find(Uuid requestId) =>
        Requests.FirstOrDefault(request => request.EvidenceRequestId == requestId);

    /// <summary>Every request in the order opened.</summary>
    public IReadOnlyList<EvidenceRequestView> ReadAll() => Requests;

    /// <summary>The requests that are fulfilled right now.</summary>
    public IReadOnlySet<Uuid> FulfilledIds() => Requests
        .Where(static request => request.Status == EvidenceRequest.Fulfilled)
        .Select(static request => request.EvidenceRequestId).ToHashSet();
}
