using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzFindingClosureWorkItemDirectoryTests
{
    delegate byte[] CorrelatedFrameEncoder(ulong correlationId, ushort messageType,
        ReadOnlySpan<byte> payload);

    readonly Uuid _tenant = Uuid.CreateVersion4();
    readonly Uuid _program = Uuid.CreateVersion4();
    readonly Uuid _finding = Uuid.CreateVersion4();
    readonly Uuid _owner = Uuid.CreateVersion4();
    readonly Uuid _actionOwner = Uuid.CreateVersion4();
    readonly Uuid _completer = Uuid.CreateVersion4();
    readonly Uuid _action = Uuid.CreateVersion4();
    readonly DateTimeOffset _now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectExactClosureRoundGivenCompletedActionsAndRevisionChanges()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        var checkpoint = new ProjectionCheckpoint(new EventCursor("finding-closure-3"));
        await ApplyAsync(directory, ProjectionCheckpoint.Start, checkpoint, Raised(), Added(), Completed());

        // Act
        var pending = await directory.LoadProjectedProgramAsync(_tenant, _program);
        var first = Assert.Single(pending.Value);
        await ApplyAsync(directory, checkpoint, checkpoint, new FindingRevised(_tenant, _program,
            _finding, 4, "medium", _owner, DueOn(), "VPN", null, "Reassessed severity.", Actor(),
            _now.AddMinutes(3)));
        var revised = await directory.LoadProjectedProgramAsync(_tenant, _program);

        // Assert
        var second = Assert.Single(revised.Value);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Equal(FindingClosureWork.Kind, second.Kind);
        Assert.Equal(_finding, second.SourceId);
        Assert.Equal(_finding, second.FindingId);
        Assert.Equal("medium", second.Materiality);
        Assert.Equal(DueOn(), second.DueOn);
        Assert.Equal("close", second.NextAction);
        Assert.Contains("revision 4", second.Reason, StringComparison.Ordinal);
        Assert.True(second.Excluded.SetEquals([_owner, _actionOwner, _completer]));
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(_tenant));
    }

    [Fact]
    public async Task ShouldRemoveClosureWorkGivenClosureAndCreateNewRoundGivenReopening()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            Raised(), Added(), Completed());
        var before = Assert.Single((await directory.LoadProjectedProgramAsync(_tenant, _program)).Value);
        var closure = new FindingClosureView(Uuid.CreateVersion4(), "Verified independently.", [],
            "All corrected.", Uuid.CreateVersion4(), Actor(), _now.AddMinutes(3), null);

        // Act
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            new FindingClosed(_tenant, _program, _finding, 4, closure));
        var closed = await directory.LoadProjectedProgramAsync(_tenant, _program);
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            new FindingReopened(_tenant, _program, _finding, 5, "Verify new source facts.",
                Actor(), _now.AddMinutes(4)));
        var reopened = await directory.LoadProjectedProgramAsync(_tenant, _program);

        // Assert
        Assert.Empty(closed.Value);
        var after = Assert.Single(reopened.Value);
        Assert.NotEqual(before.WorkItemId, after.WorkItemId);
        Assert.Equal(_now.AddMinutes(4), after.CreatedAt);
        Assert.Contains("revision 5", after.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldOmitClosureWorkGivenNoActionsOrIncompleteActions(bool addAction)
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        var events = addAction ? new DomainEvent[] { Raised(), Added() } : [Raised()];
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start, events);

        // Act
        var result = await directory.LoadProjectedProgramAsync(_tenant, _program);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ShouldRemoveClosureRoundGivenNewCorrectiveAction()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            Raised(), Added(), Completed());
        Assert.Single((await directory.LoadProjectedProgramAsync(_tenant, _program)).Value);

        // Act
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            new CorrectiveActionAdded(_tenant, _program, _finding, 4, Added().Action with
            {
                ActionId = Uuid.CreateVersion4(),
                Description = "Additional verification work.",
            }));
        var result = await directory.LoadProjectedProgramAsync(_tenant, _program);

        // Assert
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("revision")]
    [InlineData("action")]
    [InlineData("empty_member")]
    public async Task ShouldRollBackWorkAndCheckpointGivenInvalidSourceEvent(string change)
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        var checkpoint = new ProjectionCheckpoint(new EventCursor("before-invalid"));
        await ApplyAsync(directory, ProjectionCheckpoint.Start, checkpoint, Raised(), Added());
        var completion = Completed();
        completion = change switch
        {
            "tenant" => completion with { TenantId = Uuid.CreateVersion4() },
            "program" => completion with { ProgramId = Uuid.CreateVersion4() },
            "revision" => completion with { Revision = 4 },
            "empty_member" => completion with { CompletedByMemberId = Uuid.Empty },
            _ => completion with { ActionId = Uuid.CreateVersion4() },
        };

        // Act
        var error = await Record.ExceptionAsync(() => ApplyAsync(directory, checkpoint,
            new ProjectionCheckpoint(new EventCursor("invalid")), completion));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(_tenant));
        Assert.Empty((await directory.LoadProjectedProgramAsync(_tenant, _program)).Value);
    }

    [Fact]
    public async Task ShouldIsolateScopeAndReadAllPagesGivenManyClosureItems()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(), ProjectionCheckpoint.Start)))
        {
            for (var index = 0; index < 205; index++)
            {
                var finding = Uuid.CreateVersion4();
                await FindingWorkProjectionTestSource.ApplyAsync(directory, Raised() with { FindingId = finding });
                await FindingWorkProjectionTestSource.ApplyAsync(directory, Added() with { FindingId = finding });
                await FindingWorkProjectionTestSource.ApplyAsync(directory, Completed() with { FindingId = finding });
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var result = await directory.LoadProjectedProgramAsync(_tenant, _program);
        var foreignTenant = await directory.LoadProjectedProgramAsync(Uuid.CreateVersion4(), _program);
        var foreignProgram = await directory.LoadProjectedProgramAsync(_tenant, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(205, result.Value.Count);
        Assert.Equal(205, result.Value.Select(static item => item.WorkItemId).Distinct().Count());
        Assert.Empty(foreignTenant.Value);
        Assert.Empty(foreignProgram.Value);
    }

    [Fact]
    public async Task ShouldFitSlimClosureRowAndNativeEventInPinnedFitzFramesGivenMaximumActions()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzFindingClosureWorkItemDirectory(client,
            FindingWorkProjectionTestSource.Unused, TimeProvider.System);
        const string baseRoute = "kv://bdgrz/accountable-work-items/finding-closure-v3";
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(), ProjectionCheckpoint.Start)))
        {
            await FindingWorkProjectionTestSource.ApplyAsync(directory, Raised());
            for (var index = 0; index < 50; index++)
            {
                var actionId = Uuid.CreateVersion4();
                var owner = Uuid.CreateVersion4();
                var addedAt = _now.AddMinutes(1 + index * 2);
                await FindingWorkProjectionTestSource.ApplyAsync(directory,
                    new CorrectiveActionAdded(_tenant, _program, _finding, 2 + index * 2,
                        new CorrectiveActionView(actionId, "Repair", owner, DueOn(),
                            RemediationLedger.Open, Actor(), addedAt)));
                await FindingWorkProjectionTestSource.ApplyAsync(directory,
                    new CorrectiveActionCompleted(_tenant, _program, _finding,
                        3 + index * 2, actionId, new string('r', 4000),
                        [new EvidenceReference(0, "artifact", new string('a', 500),
                            new string('d', 500))], _completer, Actor(), addedAt.AddMinutes(1)));
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var route = Assert.Single(client.Operations
            .Where(static operation => operation.Operation == KvTestOperation.Insert)
            .Select(static operation => operation.Route).Distinct(StringComparer.Ordinal));
        Assert.StartsWith(baseRoute, route, StringComparison.Ordinal);
        var rowPayload = client.Snapshot(route).Select(static pair => pair.Value.ToArray())
            .First(IsFindingClosureRow);
        var nativeEvent = new CorrectiveActionCompletedV2(Uuid.CreateVersion4(), _tenant,
            _program, _finding, 3, _action, new string('r', 4000),
            [new EvidenceReference(0, "artifact", new string('a', 500),
                new string('d', 500))], _completer, Actor(), _now);
        var eventPayload = JsonSerializer.SerializeToUtf8Bytes(nativeEvent,
            ComplianceCoreJsonContext.Default.CorrectiveActionCompletedV2);

        // Act
        var rowFrame = EncodeSdkFrame(1, "KvPut", rowPayload);
        var eventFrame = EncodeSdkFrame(2, "StreamAppend", eventPayload);
        using var row = JsonDocument.Parse(rowPayload);

        // Assert
        Assert.Equal(50, row.RootElement.GetProperty("actions").GetArrayLength());
        Assert.False(row.RootElement.TryGetProperty("evidence", out _));
        Assert.False(row.RootElement.TryGetProperty("resolution_notes", out _));
        Assert.True(rowFrame.Length <= FitzLimits.MaxFrameSize);
        Assert.True(eventPayload.Length <= 48 * 1024);
        Assert.True(eventFrame.Length <= FitzLimits.MaxFrameSize);
    }

    static bool IsFindingClosureRow(byte[] payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("actions", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static byte[] EncodeSdkFrame(ulong correlationId, string messageName, byte[] payload)
    {
        var assembly = typeof(FitzLimits).Assembly;
        var codec = assembly.GetType("Cntryl.Fitz.Protocol.FrameCodec", throwOnError: true)!;
        var encoder = codec.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(static method => method.Name == "EncodeCorrelated")
            .CreateDelegate<CorrelatedFrameEncoder>();
        var messageTypes = assembly.GetType("Cntryl.Fitz.Protocol.MessageTypes", throwOnError: true)!;
        var messageType = (ushort)(messageTypes.GetField(messageName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue() ?? throw new InvalidOperationException(
                $"The pinned Fitz SDK has no {messageName} message type."));
        return encoder(correlationId, messageType, payload);
    }

    async Task ApplyAsync(FitzFindingClosureWorkItemDirectory directory,
        ProjectionCheckpoint previous, ProjectionCheckpoint next, params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(), previous));
        foreach (var ev in events)
            await FindingWorkProjectionTestSource.ApplyAsync(directory, ev);
        await batch.CommitAsync(next);
    }

    CheckpointIdentity Identity() => new(FitzFindingClosureWorkItemDirectory.ProjectorName,
        EventStreamPattern.ForPattern(_tenant.ToString()));
    static ActorReference Actor() => ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static DateOnly DueOn() => new(2026, 10, 9);
    FindingRaised Raised() => new(_tenant, _program, _finding, 1,
        new FindingSource("manual", null, null, "Stale access."), "Stale access", "Old accounts.",
        "high", "VPN", _owner, DueOn(), [], Actor(), _now);
    CorrectiveActionAdded Added() => new(_tenant, _program, _finding, 2,
        new CorrectiveActionView(_action, "Remove accounts", _actionOwner, DueOn(),
            RemediationLedger.Open, Actor(), _now.AddMinutes(1)));
    CorrectiveActionCompleted Completed() => new(_tenant, _program, _finding, 3,
        _action, "Removed", [], _completer, Actor(), _now.AddMinutes(2));
}
