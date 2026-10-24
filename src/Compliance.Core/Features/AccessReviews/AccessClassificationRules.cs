using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

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
