using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Rules;

/// <summary>
///     The identity-shape rules of an explicit classification (M0-D07). Human requires a person, NHI a governed
///     service identity, and shared an accountable owner and justification.
/// </summary>
static class AccessClassificationRules
{
    public static RequestError? Validate(string classification, string rationale, Uuid? personId,
        Uuid? serviceIdentityId, Uuid? ownerPersonId, string? justification)
    {
        if (!AccessReviewVocabulary.Classifications.Contains(classification ?? string.Empty))
            return new(RequestErrorKind.Validation,
                "The classification must be human, nhi, shared, or unclassified.");
        if (!AccessReviewVocabulary.IsBoundedText(rationale, 2000))
            return new(RequestErrorKind.Validation,
                "A classification requires a rationale of at most 2000 characters.");
        var valid = classification switch
        {
            AccessReviewVocabulary.Human => personId is not null && serviceIdentityId is null &&
                                            ownerPersonId is null && justification is null,
            AccessReviewVocabulary.Nhi => serviceIdentityId is not null && personId is null &&
                                          ownerPersonId is null && justification is null,
            AccessReviewVocabulary.Shared => ownerPersonId is not null && personId is null &&
                                             serviceIdentityId is null &&
                                             AccessReviewVocabulary.IsBoundedText(justification, 2000),
            _ => personId is null && serviceIdentityId is null && ownerPersonId is null &&
                 justification is null,
        };
        return valid
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "Human requires only a person, nhi only a service identity, shared an accountable owner and justification, and unclassified none.");
    }
}
