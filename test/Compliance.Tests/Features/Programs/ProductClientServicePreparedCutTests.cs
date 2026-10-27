using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProductClientServicePreparedCutTests
{
    [Fact]
    public async Task ShouldFenceCoherentOtherSavedPrefixGivenPrepareAndExecutorUseDifferentSources()
    {
        // Arrange
        await using var first = new ProductClientServiceFixture();
        var tenant = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var rules = new ProductClientServiceRules(tenant, service);
        var member = Uuid.CreateVersion4();
        var at = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.True(rules.Create(Uuid.CreateVersion4(), "Payroll", "Process payroll", "Operations", member, "Author", at).IsSuccess);
        Assert.Null(rules.Revise(1, "Second", "Purpose", "Owner", member, "Author", at.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", "Purpose", "Owner", member, "Author", at.AddMinutes(2)));
        await first.PublishAsync(rules);
        var original = await first.Services.HistoryAsync(tenant, service);
        var facts = original.Facts.ToArray();
        var intermediate = Assert.IsType<ClientServiceRevised>(facts[1]);
        var changed = intermediate with { };
        changed.AttachMetadata(intermediate.Metadata with { CausationId = Uuid.CreateVersion4() });
        facts[1] = changed;
        var events = new InMemoryEventStore();
        await events.AppendAsync(original.Owner.Stream, 0, facts);
        await using var second = new ProductClientServiceFixture(events);
        var other = await second.Services.HistoryAsync(tenant, service);
        Assert.Equal(original.Owner.Version, other.Owner.Version);
        Assert.Equal(original.Owner.CommittedStreamPosition, other.Owner.CommittedStreamPosition);
        Assert.Equal(original.Owner.NativeCut.Digest, other.Owner.NativeCut.Digest);
        Assert.NotEqual(original.Owner.NativeCut.MetadataDigest, other.Owner.NativeCut.MetadataDigest);
        Assert.Equal(original.Owner.CurrentContent, other.Owner.CurrentContent);
        Assert.Equal(original.Owner.ProgramId, other.Owner.ProgramId);
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var request = new ReviseClientService(Uuid.CreateVersion4(), tenant, service, 3, "Fourth", "Purpose", "Owner");
        var mixed = new ProductClientServiceOperations(new NativeOperationExecutor(first.Reader, second.Executor, first.Content),
            first.Reader, first.Content, TimeProvider.System);

        // Act
        var refused = await mixed.ReviseAsync(new RequestContext<ReviseClientService>(request, actor), CancellationToken.None);
        var afterRefusal = await second.Services.HistoryAsync(tenant, service);
        var current = new ProductClientServiceOperations(new NativeOperationExecutor(second.Reader, second.Executor, first.Content),
            second.Reader, first.Content, TimeProvider.System);
        var retried = await current.ReviseAsync(new RequestContext<ReviseClientService>(request, actor), CancellationToken.None);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.Equal(3, afterRefusal.Facts.Count);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal(4, (await second.Services.HistoryAsync(tenant, service)).Facts.Count);
    }
}
