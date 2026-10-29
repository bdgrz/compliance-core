using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     A system instance's expectations, expectation exceptions, and population exceptions, composed from their
///     independent owners. There is no system-wide revision: each expectation carries its own line revision.
/// </summary>
public sealed record AccessExpectationsView(Uuid TenantId, Uuid SystemInstanceId,
    IReadOnlyList<AccessExpectationView> Expectations,
    IReadOnlyList<AccessExpectationExceptionView> Exceptions,
    IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions);
