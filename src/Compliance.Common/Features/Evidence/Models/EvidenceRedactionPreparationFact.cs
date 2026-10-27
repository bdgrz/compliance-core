using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Evidence.Models;

public sealed record EvidenceRedactionPreparationFact(Uuid PreparationId, long Revision,
    EvidenceRedactionSourceCapsule Original, EvidenceRedactionSourceCapsule Derived,
    string Provenance, string Reason, ActorReference PreparedBy, DateTimeOffset PreparedAt);
