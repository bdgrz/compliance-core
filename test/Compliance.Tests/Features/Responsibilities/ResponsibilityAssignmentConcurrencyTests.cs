using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilityAssignmentConcurrencyTests
{
    [Fact]
    public async Task ShouldPersistOnlyOneConflictingAssignmentGivenConcurrentCommands()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var memberId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        await using var native = new BoundaryProjectionScenario();
        var reader = native.Reader;
        var boundary = new BoundaryRules(tenantId, boundaryId);
        Assert.True(boundary.Create(Uuid.CreateVersion4(), versionId,
            new BoundaryContent("Statement", "readiness", ["security"], []), Uuid.CreateVersion4(),
            "Author", now).IsSuccess);
        await native.PublishAsync(boundary);
        var gate = new HydrationGate(reader, participants: 2);

        var scope = new ResponsibilityScope("boundary", boundaryId, versionId, 1);

        // Act
        var outcomes = await Task.WhenAll(
            RaceAsync(native, gate, tenantId, boundaryId, scope, memberId,
                ResponsibilityType.ControlOwner, now),
            RaceAsync(native, gate, tenantId, boundaryId, scope, memberId,
                ResponsibilityType.AssignedReviewer, now));
        var resultingBoundary = await native.Boundaries.ReadAsync(tenantId, boundaryId);
        var assignments = resultingBoundary.GetResponsibilitySet(scope).ReadAssignments();

        // Assert
        Assert.Single(outcomes, static outcome => outcome is Result { IsSuccess: true });
        Assert.Single(outcomes, static outcome => outcome is EventStreamConcurrencyException);
        Assert.Single(assignments);
    }

    static async Task<object> RaceAsync(BoundaryProjectionScenario native, IAggregateReader gate, Uuid tenantId,
        Uuid boundaryId, ResponsibilityScope scope, Uuid memberId,
        ResponsibilityType type, DateTimeOffset now)
    {
        try
        {
            var rules = await native.Boundaries.RulesAsync(tenantId, boundaryId);
            var failure = rules.AssignResponsibility(scope, Uuid.CreateVersion4(), memberId,
                type, Uuid.CreateVersion4(), "Administrator", now, now, null, []);
            if (failure is not null)
                return Result.Failure(failure.ToRequestError());
            await BoundarySeeds.AppendAsync(gate, native.Writer, native.Content, rules);
            return Result.Success;
        }
        catch (EventStreamConcurrencyException exception)
        {
            return exception;
        }
    }

    sealed class HydrationGate(IAggregateReader inner, int participants) : IAggregateReader
    {
        readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrived;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is SystemBoundary)
            {
                if (Interlocked.Increment(ref _arrived) == participants)
                    _released.TrySetResult();
                await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            return hydrated;
        }
    }
}
