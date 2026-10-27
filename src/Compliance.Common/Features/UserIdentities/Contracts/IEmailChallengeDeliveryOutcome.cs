using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

/// <summary>
///     A typed outcome of one delivery attempt of an email challenge. Outcomes are recorded by the named delivery
///     process principal only; an outcome of an attempt that a newer attempt has replaced is ignored.
/// </summary>
public interface IEmailChallengeDeliveryOutcome : IRequest, IIdentifiedRequest
{
    /// <summary>The deterministic command id of this outcome: one per source fact, effect and attempt.</summary>
    Uuid CommandId { get; }

    /// <summary>The challenge being delivered; the delivery ledger is keyed by it.</summary>
    Uuid ChallengeId { get; }

    /// <summary>The attempt the outcome reports on.</summary>
    Uuid AttemptId { get; }
}
