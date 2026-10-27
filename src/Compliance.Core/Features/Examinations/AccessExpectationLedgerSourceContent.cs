using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record AccessExpectationLedgerSourceContent(DateOnly AsOf, Uuid SystemInstanceId,
    IReadOnlyList<AccessExpectationView> Expectations,
    IReadOnlyList<AccessExpectationExceptionView> Exceptions,
    IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions);
