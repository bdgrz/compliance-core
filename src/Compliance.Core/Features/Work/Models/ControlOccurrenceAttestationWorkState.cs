using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Work.Models;

public sealed record ControlOccurrenceAttestationWorkState(Uuid AttestationId, int Version,
    Uuid RecorderMemberId, OperatingHolder PerformedBy, DateTimeOffset RecordedAt);
