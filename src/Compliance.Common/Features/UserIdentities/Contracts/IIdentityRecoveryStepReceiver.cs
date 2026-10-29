using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

/// <summary>A receiver that mutates one owner on behalf of the recovery and names the generation and intent it acts for.</summary>
public interface IIdentityRecoveryStepReceiver : IIdentityRecoveryReceiver
{
    /// <summary>The native stream the receiver mutates.</summary>
    EventStreamAddress Target { get; }

    /// <summary>The root operation of the recovery, which equals the challenge.</summary>
    Uuid OperationId { get; }

    /// <summary>The recovery generation of the source fact.</summary>
    long Generation { get; }

    /// <summary>The authored revision of the target; null when the receiver does not constrain it.</summary>
    long? ExpectedRevision { get; }

    /// <summary>The frozen identity recovery intent.</summary>
    ImmutableContentRef Intent { get; }
}
