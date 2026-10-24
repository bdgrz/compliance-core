namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Maps authored wire strings of the independence rules to policy values and policy decisions back to wire codes.</summary>
static class IndependenceWire
{
    public static IndependenceServiceClassification Classification(string value) => value switch
    {
        "compatible" => IndependenceServiceClassification.Compatible,
        "conditionally_compatible" => IndependenceServiceClassification.ConditionallyCompatible,
        "impairing" => IndependenceServiceClassification.Impairing,
        _ => throw new InvalidOperationException("An authored classification was invalid."),
    };

    public static string Code(IndependenceDecisionCode code) => code switch
    {
        IndependenceDecisionCode.Allowed => "allowed",
        IndependenceDecisionCode.RecentImpairingService => "recent_impairing_service",
        IndependenceDecisionCode.PartnerEvaluationRequired => "partner_evaluation_required",
        IndependenceDecisionCode.ServiceNotClassified => "service_not_classified",
        _ => throw new InvalidOperationException("An invalid policy result cannot be retained as an evaluation."),
    };
}
