using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The shared half of every acquire pair of the engagement personal decision and acceptance processes: the source
///     is verified from the engagement owner, and a pinned source that is no longer eligible rejects the whole process
///     so the abort receiver fences authorization and every use, held or not, is settled as canceled.
/// </summary>
/// <typeparam name="TOwner">The source owner that embeds <see cref="SourceUseReservations" />.</typeparam>
/// <typeparam name="TRequest">The pair's authorize command.</typeparam>
abstract class EngagementUseAuthorizeHandler<TOwner, TRequest>(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : AuthorizeSourceUseHandler<TOwner, TRequest>(reader, executor, content, clock)
    where TOwner : Aggregate, ISourceUseOwner
    where TRequest : ISourceUseAuthorization, IEngagementPersonalDecisionUse
{
    /// <summary>The command kind tagging the grant fact.</summary>
    protected abstract string CommandKind { get; }

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(TRequest request, CancellationToken ct) =>
        verifier.AuthorizeSourceAsync(request, CommandKind, ct);

    protected override ValueTask<Result<ReceiverOutcome>> RefuseAsync(TRequest request, SourceUseSource source,
        string reason, IExecutionContext context, CancellationToken ct) => ValueTask.FromResult(
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason, null)));

    /// <summary>A pinned source that is not eligible any more fails the whole decision permanently.</summary>
    protected static Result Stale(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message,
        false));

    /// <summary>A pinned source that is not visible yet is waited for.</summary>
    protected static Result Waiting(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        message, true));
}
