using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskTreatmentView(string Kind, string Rationale, ActorReference ChosenBy,
    DateTimeOffset ChosenAt);
