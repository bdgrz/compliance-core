using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Rules;

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
