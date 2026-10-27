using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskTreatmentView(string Kind, string Rationale, ActorReference ChosenBy,
    DateTimeOffset ChosenAt);
