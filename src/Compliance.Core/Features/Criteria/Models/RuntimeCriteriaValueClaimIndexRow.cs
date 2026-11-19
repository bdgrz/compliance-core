using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One source-observed operation claim for an independent metadata value.</summary>
sealed record RuntimeCriteriaValueClaimIndexRow(string Generation, Uuid OwnerId, Uuid EventId,
    CriteriaEditionValueIntent Intent, long ExecutionGeneration, ActorAttribution Actor, Uuid UserId);
