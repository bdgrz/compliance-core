using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Complete verified client service and evaluation history, composed outside current authority.</summary>
public sealed class ClientServiceHistorySnapshot(ClientServiceIndependence owner,
    IReadOnlyList<NonattestServiceRecorded> services, IReadOnlyList<ClientIndependenceEvaluationAdmitted> admissions)
{
    public ClientServiceIndependence Owner { get; } = owner;
    public IReadOnlyList<NonattestServiceRecorded> Services { get; } = services;
    public IReadOnlyList<ClientIndependenceEvaluationAdmitted> Admissions { get; } = admissions;
    public ClientIndependenceEvaluationAdmitted? AdmissionOf(Uuid id) => Admissions.SingleOrDefault(value => value.Operation.LogicalId == id);
}
