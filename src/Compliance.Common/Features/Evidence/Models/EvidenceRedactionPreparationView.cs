using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Evidence.Models;

public sealed record EvidenceRedactionPreparationView(Uuid PreparationId, long Revision,
    EvidenceRedactionArtifactView Original, EvidenceRedactionArtifactView Derived,
    string Provenance, string Reason, ActorReference PreparedBy, DateTimeOffset PreparedAt);
