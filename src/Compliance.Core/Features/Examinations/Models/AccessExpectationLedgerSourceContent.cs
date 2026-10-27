using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record AccessExpectationLedgerSourceContent(DateOnly AsOf, Uuid SystemInstanceId,
    IReadOnlyList<AccessExpectationView> Expectations,
    IReadOnlyList<AccessExpectationExceptionView> Exceptions,
    IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions);
