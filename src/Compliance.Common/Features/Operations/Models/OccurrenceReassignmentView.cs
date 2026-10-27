using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>An explicit reassignment of open work when ownership changed.</summary>
public sealed record OccurrenceReassignmentView(Uuid OccurrenceId, OperatingHolder? From,
    OperatingHolder To, Uuid PlanVersionId);
