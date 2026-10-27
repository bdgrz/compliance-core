using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>Why a variance item has its category, with the expectation or exception that explains it.</summary>
public sealed record AccessVarianceFinding(string Kind, Uuid? ExpectationId, Uuid? ExceptionId,
    string Explanation);
