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
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The shared half of every settle pair of the engagement personal decision and acceptance processes. The terminal
///     fact is resolved from the engagement owner: an authorization or a recorded acceptance requires
///     <c>irreversible_decision</c>, a cancellation or a rejection requires <c>canceled</c>. A missing terminal fact is a
///     transient wait, never an inferred cancellation.
/// </summary>
/// <typeparam name="TOwner">The source owner that embeds <see cref="SourceUseReservations" />.</typeparam>
/// <typeparam name="TRequest">The pair's settle command.</typeparam>
abstract class EngagementUseSettleHandler<TOwner, TRequest>(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : SettleSourceUseHandler<TOwner, TRequest>(reader, executor, content, clock)
    where TOwner : Aggregate, ISourceUseOwner
    where TRequest : ISourceUseSettlement, IEngagementPersonalDecisionUse
{
    /// <summary>The command kind tagging the settlement fact.</summary>
    protected abstract string CommandKind { get; }

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(TRequest request, CancellationToken ct) =>
        verifier.SettleSourceAsync(request, CommandKind, ct);
}
