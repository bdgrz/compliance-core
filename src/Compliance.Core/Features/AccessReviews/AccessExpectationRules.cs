using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The rule-shape validation and normalization of an access expectation proposal.</summary>
static class AccessExpectationRules
{
    public static string? ValidateRule(string ruleKind, AccessExpectationParameters? parameters)
    {
        if (parameters is null || !AccessReviewVocabulary.RuleKinds.Contains(ruleKind ?? string.Empty))
            return "The rule kind must be forbidden_principal_kind, privileged_entitlement, requires_expiry, or required_access.";
        if (parameters.PrincipalKind is { } kind && !AccessReviewVocabulary.PrincipalKinds.Contains(kind))
            return "The principal kind is not recognized.";
        if (parameters.EntitlementKind is { } entitlementKind &&
            !AccessReviewVocabulary.EntitlementKinds.Contains(entitlementKind))
            return "The entitlement kind is not recognized.";
        if (parameters.ProviderEntitlementId is { Length: 0 or > 512 } ||
            parameters.ProviderSubjectId is { Length: 0 or > 512 })
            return "Provider IDs must be between 1 and 512 characters.";
        var entitlementMatch = parameters.EntitlementKind is not null ||
                               parameters.ProviderEntitlementId is not null;
        return ruleKind switch
        {
            AccessReviewVocabulary.ForbiddenPrincipalKind when parameters.PrincipalKind is null ||
                                                                entitlementMatch ||
                                                                parameters.ProviderSubjectId is not null =>
                "A forbidden_principal_kind rule names only a principal kind.",
            AccessReviewVocabulary.PrivilegedEntitlement or AccessReviewVocabulary.RequiresExpiry
                when !entitlementMatch || parameters.ProviderSubjectId is not null =>
                "This rule names an entitlement kind or provider entitlement ID, optionally narrowed by principal kind.",
            AccessReviewVocabulary.RequiredAccess when parameters.ProviderSubjectId is null ||
                                                        parameters.ProviderEntitlementId is null ||
                                                        parameters.EntitlementKind is not null ||
                                                        parameters.PrincipalKind is not null =>
                "A required_access rule names exactly one provider subject and provider entitlement.",
            _ => null,
        };
    }

    public static AccessExpectationParameters Normalize(AccessExpectationParameters parameters) =>
        new(parameters.PrincipalKind, parameters.EntitlementKind, parameters.ProviderEntitlementId,
            parameters.ProviderSubjectId);
}
