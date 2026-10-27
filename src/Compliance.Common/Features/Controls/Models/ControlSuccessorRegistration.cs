using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlSuccessorRegistration(Uuid ControlId, Uuid DraftVersionId,
    Uuid PredecessorVersionId, long Revision);
