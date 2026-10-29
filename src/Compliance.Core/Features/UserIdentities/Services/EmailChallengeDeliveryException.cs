using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

/// <summary>
///     A challenge message was not accepted by the relay. <see cref="Certainty" /> says whether that is known: only an
///     explicit relay rejection proves non-acceptance, so any other exception a delivery throws is treated as
///     <see cref="EmailChallengeFailureCertainty.Unknown" />. The message is generic: relay replies can echo recipients.
/// </summary>
public sealed class EmailChallengeDeliveryException(string certainty, string failureCode)
    : Exception("Email challenge delivery failed.")
{
    /// <summary>One of <see cref="EmailChallengeFailureCertainty" />.</summary>
    public string Certainty { get; } = EmailChallengeFailureCertainty.IsDefined(certainty)
        ? certainty
        : throw new ArgumentOutOfRangeException(nameof(certainty));

    /// <summary>The redacted failure category.</summary>
    public string FailureCode { get; } = failureCode;
}
