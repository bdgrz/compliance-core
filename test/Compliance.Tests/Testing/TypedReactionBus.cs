using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Lets a reactor under <see cref="ReactorScenario" /> send commands that answer with a typed response. The
///     scenario's own bus records only requests without a result, so each typed command is recorded as a
///     <see cref="TypedRequest" /> in the scenario's ordered list and answered here; <see cref="Sent" /> reads the list
///     back with the original commands in place.
/// </summary>
sealed class TypedReactionBus(IRequestBus inner) : IRequestBus
{
#pragma warning disable IDE0028 // ConditionalWeakTable cannot take a collection expression.
    static readonly ConditionalWeakTable<ReactorScenario, TypedReactionBus> Buses = new();
#pragma warning restore IDE0028
    readonly Dictionary<Type, RequestError> _failures = [];

    /// <summary>The recording wrapper a typed command takes in the scenario's list of sent requests.</summary>
    public sealed record TypedRequest([property: JsonIgnore] IRequestBase Request) : IRequest;

    /// <summary>The bus a reactor under test is constructed with.</summary>
    public static IRequestBus For(ReactorScenario scenario) => Buses.GetValue(scenario, s => new TypedReactionBus(s.Requests));

    /// <summary>Scripts a failure for every later <typeparamref name="TRequest" />.</summary>
    public static void FailWith<TRequest>(ReactorScenario scenario, RequestError error) =>
        ((TypedReactionBus)For(scenario))._failures[typeof(TRequest)] = error;

    public static void Succeed<TRequest>(ReactorScenario scenario) =>
        ((TypedReactionBus)For(scenario))._failures.Remove(typeof(TRequest));

    /// <summary>Every command the reactor sent, in order, with typed commands unwrapped.</summary>
    public static IReadOnlyList<IRequestBase> Sent(ReactorScenario scenario) => scenario.SentRequests
        .Select(request => request is TypedRequest typed ? typed.Request : request).ToList();

    public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
        inner.CreateContext(actor, metadata);

    public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
        CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);

    public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
        CancellationToken ct = default) => inner.DispatchAsync(request, context, ct);

    public async ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default)
    {
        // The recording wrapper never leaves the in-memory scenario bus, so it needs no serializer root.
        await inner.DispatchAsync(new TypedRequest(request), context, ct);
        if (_failures.TryGetValue(request.GetType(), out var error))
            return Result<TOut>.Failure(error);
        if (typeof(TOut) == typeof(UnitResult))
            return Result<TOut>.Success((TOut)(object)new UnitResult());
        if (typeof(TOut) != typeof(AdministratorTransitionResponse))
            throw new NotSupportedException($"No scripted response for {typeof(TOut).Name}.");
        return Result<TOut>.Success((TOut)(object)new AdministratorTransitionResponse(Uuid.CreateVersion4(),
            AdministratorTransitionResponseState.Completed, null, DateTimeOffset.UnixEpoch));
    }

    public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default) =>
        inner.DispatchStreamAsync(request, context, ct);
}
