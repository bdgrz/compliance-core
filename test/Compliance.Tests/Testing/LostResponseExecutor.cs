using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Commits through the inner executor and then loses the response once, so the caller cannot tell whether the
///     append happened: the unknown append outcome a retry must resolve without writing twice.
/// </summary>
sealed class LostResponseExecutor(IAggregateExecutor inner) : IAggregateExecutor
{
    bool _lost;

    public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
        Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
        CancellationToken ct = default) where TAggregate : Aggregate
    {
        var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
        return Lose(result);
    }

    public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
        Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
        CancellationToken ct = default) where TAggregate : Aggregate
    {
        var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
        return Lose(result);
    }

    TResult Lose<TResult>(TResult result)
    {
        if (_lost)
            return result;
        _lost = true;
        throw new TimeoutException("The append response was lost.");
    }
}
