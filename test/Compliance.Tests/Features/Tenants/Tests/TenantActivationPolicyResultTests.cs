using System.Security.Claims;
using Cntryl.Portia;
using Microsoft.Extensions.Logging.Abstractions;
using Bdgrz.Compliance.Features.Tenants.Rules;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantActivationPolicyResultTests
{
    [Fact]
    public async Task ShouldRetryActivationGivenTransientReturnedFailure()
    {
        // Arrange
        var error = new RequestError(RequestErrorKind.Conflict, "Provisioning is pending.", isTransient: true);
        var bus = new ScriptedBus(attempt => attempt == 1 ? Result.Failure(error) : Result.Success);
        var policy = new TenantActivationPolicy(TimeSpan.FromSeconds(2));
        var context = Context();
        var activation = new ActivateTenant(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = await policy.SendActivationAsync(bus, activation, context, NullLogger.Instance, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, bus.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnExactFailureGivenPermanentRefusalOrExhaustedTransientWindow(bool transient)
    {
        // Arrange
        var error = new RequestError(RequestErrorKind.Conflict, "Activation is unavailable.", isTransient: transient);
        var bus = new ScriptedBus(_ => Result.Failure(error));
        var policy = new TenantActivationPolicy(TimeSpan.Zero);
        var activation = new ActivateTenant(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = await policy.SendActivationAsync(bus, activation, Context(), NullLogger.Instance,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Equal(1, bus.Attempts);
    }

    [Fact]
    public async Task ShouldAbandonOnlyAtBudgetGivenReturnedPermanentRefusals()
    {
        // Arrange
        var policy = new TenantActivationPolicy(TimeSpan.Zero, permanentFailureLimit: 2);
        var context = Context();
        var error = new RequestError(RequestErrorKind.Conflict, "The administrator is ineligible.");

        // Act
        var first = await ReactAsync(policy, context, Result.Failure(error));
        var second = await ReactAsync(policy, context, Result.Failure(error));

        // Assert
        Assert.False(first.IsSuccess);
        Assert.Same(error, first.Error);
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task ShouldResetPermanentBudgetGivenSuccessfulReactionBetweenFailures()
    {
        // Arrange
        var policy = new TenantActivationPolicy(TimeSpan.Zero, permanentFailureLimit: 2);
        var context = Context();
        var refusal = Result.Failure(new RequestError(RequestErrorKind.Conflict, "The administrator is ineligible."));

        // Act
        var first = await ReactAsync(policy, context, refusal);
        var recovery = await ReactAsync(policy, context, Result.Success);
        var afterRecovery = await ReactAsync(policy, context, refusal);
        var exhausted = await ReactAsync(policy, context, refusal);

        // Assert
        Assert.False(first.IsSuccess);
        Assert.True(recovery.IsSuccess);
        Assert.False(afterRecovery.IsSuccess);
        Assert.True(exhausted.IsSuccess);
    }

    [Fact]
    public async Task ShouldPreservePermanentBudgetGivenInterveningTransientFailures()
    {
        // Arrange
        var policy = new TenantActivationPolicy(TimeSpan.Zero, permanentFailureLimit: 2);
        var context = Context();
        var permanent = Result.Failure(new RequestError(RequestErrorKind.Conflict, "The administrator is ineligible."));
        var transient = Result.Failure(new RequestError(RequestErrorKind.Conflict, "Provisioning is pending.",
            isTransient: true));

        // Act
        var first = await ReactAsync(policy, context, permanent);
        var pending = new List<Result>();
        for (var attempt = 0; attempt < 3; attempt++)
            pending.Add(await ReactAsync(policy, context, transient));
        var exhausted = await ReactAsync(policy, context, permanent);

        // Assert
        Assert.False(first.IsSuccess);
        Assert.All(pending, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Same(transient.Error, result.Error);
        });
        Assert.True(exhausted.IsSuccess);
    }

    static ValueTask<Result> ReactAsync(TenantActivationPolicy policy, RecordReactorContext context, Result result) =>
        policy.ReactAsync("Activation", ((TenantRegistered)context.Source.Event).TenantId, context.Source.Event,
            NullLogger.Instance, _ => ValueTask.FromResult(result), CancellationToken.None);

    static RecordReactorContext Context()
    {
        var tenantId = Uuid.CreateVersion4();
        var trigger = new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Tenant", "tenant");
        trigger.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), tenantId, 1,
            DateTimeOffset.UnixEpoch));
        return new RecordReactorContext(new DomainEventRecord(new EventStreamAddress(tenantId.ToString(),
            "tenants", tenantId.ToString()), trigger, 0, new EventCursor("1")));
    }

    sealed class ScriptedBus(Func<int, Result> response) : IRequestBus
    {
        public int Attempts { get; private set; }

        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            new(actor, metadata: metadata);

        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);

        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default) => ValueTask.FromResult(response(++Attempts));

        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
