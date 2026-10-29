using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

/// <summary>What the two typed delivery outcome commands share: the failure code shape and the decision mapping.</summary>
static class EmailChallengeDeliveryOutcomes
{
    /// <summary>A failure code is a short lowercase category, never a provider message.</summary>
    public static bool IsFailureCode(string? code) => code is { Length: > 0 and <= 64 } &&
                                                      code.All(character => char.IsAsciiLetterLower(character) ||
                                                                            char.IsAsciiDigit(character) || character == '_');

    /// <summary>A recorded, replayed, settled or stale outcome succeeds; a contradiction or an unknown attempt conflicts.</summary>
    public static Result ToResult(EmailChallengeOutcomeDecision decision) => decision.Kind switch
    {
        EmailChallengeOutcomeKind.Closed or EmailChallengeOutcomeKind.Replayed or EmailChallengeOutcomeKind.Resolved
            or EmailChallengeOutcomeKind.Stale => Result.Success,
        EmailChallengeOutcomeKind.UnknownAttempt => Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The delivery outcome names an attempt this challenge never opened.", false)),
        _ => Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The attempt already holds a different delivery outcome.", false)),
    };
}
