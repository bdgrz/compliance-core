using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A system instance's expectations, expectation exceptions and missing-population exceptions composed from their
///     independent owners at read time. System-level coverage is a read model; nothing here is an atomic owner.
/// </summary>
public sealed class AccessExpectationsSnapshot(Uuid tenantId, Uuid systemInstanceId,
    IReadOnlyList<AccessExpectationView> expectations, IReadOnlyList<AccessExpectationExceptionView> exceptions,
    IReadOnlyList<AccessPopulationExceptionView> populationExceptions)
{
    /// <summary>Every expectation of every line.</summary>
    public IReadOnlyList<AccessExpectationView> Expectations => expectations;

    /// <summary>Every expectation exception.</summary>
    public IReadOnlyList<AccessExpectationExceptionView> Exceptions => exceptions;

    /// <summary>Every missing-population exception.</summary>
    public IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions => populationExceptions;

    /// <summary>The composed view.</summary>
    public AccessExpectationsView ToView() => new(tenantId, systemInstanceId, [.. expectations], [.. exceptions],
        [.. populationExceptions]);

    /// <summary>
    ///     Approved expectations effective at <paramref name="at" />. An approved successor ends its
    ///     predecessor from the successor's effective date.
    /// </summary>
    public IReadOnlyList<AccessExpectationView> EffectiveAt(DateTimeOffset at) =>
        expectations.Where(expectation => expectation.Status == AccessExpectation.Approved &&
                                          expectation.EffectiveFrom <= at &&
                                          (expectation.EffectiveUntil is null || at < expectation.EffectiveUntil) &&
                                          !expectations.Any(successor =>
                                              successor.Status == AccessExpectation.Approved &&
                                              successor.SupersedesExpectationId == expectation.ExpectationId &&
                                              successor.EffectiveFrom <= at))
            .ToArray();

    /// <summary>The latest missing-population exception that is approved and unexpired at <paramref name="at" />.</summary>
    public AccessPopulationExceptionView? PopulationExceptionAt(DateTimeOffset at) =>
        populationExceptions.LastOrDefault(exception => exception.ApprovedAt <= at && at < exception.ExpiresAt);
}
