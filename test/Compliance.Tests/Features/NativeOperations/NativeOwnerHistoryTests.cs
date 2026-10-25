using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class NativeOwnerHistoryTests
{
    static readonly TrainingRequirementContent Content = new("Security awareness", "Complete original content",
        PolicyAudience.CoreSecurity, null, "manual");

    [Fact]
    public async Task ShouldExcludeAuditSessionFactsGivenNativeHistoryAtCapturedOwnerCut()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, "TR-SEC");
        var auditId = Uuid.CreateVersion4();
        var stream = new EventStreamAddress(host.Tenant.ToString(), "audit-sessions", auditId.ToString());
        DomainEvent audit = new HistoryAudit();
        audit.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), auditId, 0, host.Clock.GetUtcNow(), IsAudit: true));
        await host.Provider.GetRequiredService<IEventStore>().AppendAsync(stream, 0, [audit]);
        var result = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC", Content),
            host.Manager);
        Assert.True(result.IsSuccess, result.Error?.Message);
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));

        // Act
        var facts = await TrainingRequirementHistory.FactsAsync(host.Events, owner);
        var view = await TrainingRequirementRules.ViewAsync(host.Content, host.Events, owner, 1, CancellationToken.None);

        // Assert
        Assert.Equal(1UL, owner.CommittedStreamPosition);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(1, Assert.Single(facts).Version);
        Assert.Equal(Content.CourseName, view!.Content.CourseName);
    }

    [Fact]
    public async Task ShouldKeepOriginalHistoryCutGivenSuccessorCommitsDuringReadComposition()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var captured = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        await TrainingSeed.ReviseAsync(host.Provider, host.Tenant, id, "TR-SEC", 1,
            Content with { CourseName = "Successor" }, host.ManagerActor, host.Clock.GetUtcNow());

        // Act
        var facts = await TrainingRequirementHistory.FactsAsync(host.Events, captured);
        var view = await TrainingRequirementRules.ViewAsync(host.Content, host.Events, captured, null, CancellationToken.None);
        var current = await host.SendAsync(new GetTrainingRequirement(host.Tenant, host.Program, id), host.Manager);

        // Assert
        Assert.Equal(1, Assert.Single(facts).Version);
        Assert.Equal(1, view!.LatestVersion);
        Assert.Equal(Content.CourseName, view.Content.CourseName);
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal(2, current.Value.Version);
        Assert.Equal("Successor", current.Value.Content.CourseName);
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("foreign_stream", false)]
    [InlineData("physical_gap", false)]
    public async Task ShouldClassifyExactSourceFailureGivenMissingOrContradictoryHistory(string fault, bool transient)
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        var reader = new AlteredReader(host.Events, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with
            {
                Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(),
                record.Stream.Area, record.Stream.Resource)
            },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            _ => record,
        });

        // Act
        var error = await Record.ExceptionAsync(async () => await TrainingRequirementHistory.FactsAsync(reader, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(error);
        Assert.Equal(transient, error is ImmutableContentUnavailableException);
    }

    [Fact]
    public async Task ShouldRejectWrongLogicalBodyDigestGivenVerifiedImmutableRequirementBytes()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        var fact = Assert.IsType<TrainingRequirementDefined>(owner.Latest);

        // Act
        var error = await Record.ExceptionAsync(async () => await TrainingRequirementRules.ViewAsync(host.Content,
            fact with { ContentSha256 = new string('f', 64) }, 1, CancellationToken.None));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
    }

    sealed record HistoryAudit : DomainEvent;

    sealed class AlteredReader(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
    }
}
