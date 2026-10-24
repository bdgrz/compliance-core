using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The actor, performer, snapshot, current occurrence view and plan an occurrence proof is decided on.</summary>
sealed record PreparedOccurrenceProof(OperationsActor Actor, OperatingHolder Performer,
    IReadOnlyDictionary<Uuid, DateOnly?> Windows, ControlOperationsSnapshot Snapshot,
    ControlOccurrenceView Current, ControlOperatingPlanView Plan);
