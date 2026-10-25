using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Complete verified client service and evaluation history, composed outside current authority.</summary>
public sealed class ClientServiceHistorySnapshot(ClientServiceIndependence owner,
    IReadOnlyList<NonattestServiceRecorded> services, IReadOnlyList<ClientIndependenceEvaluationAdmitted> admissions)
{
    public ClientServiceIndependence Owner { get; } = owner;
    public IReadOnlyList<NonattestServiceRecorded> Services { get; } = services;
    public IReadOnlyList<ClientIndependenceEvaluationAdmitted> Admissions { get; } = admissions;
    public ClientIndependenceEvaluationAdmitted? AdmissionOf(Uuid id) => Admissions.SingleOrDefault(value => value.Operation.LogicalId == id);
}
