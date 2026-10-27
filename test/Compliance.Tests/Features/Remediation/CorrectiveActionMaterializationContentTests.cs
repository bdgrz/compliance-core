using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class CorrectiveActionMaterializationContentTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyImmutableAdmissionFailureGivenMaterializationContentIsUnavailableOrCorrupt(bool unavailable)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var findingId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var target = new CorrectiveAction(fixture.TenantId, fixture.ProgramId, findingId, actionId);
        var command = new MaterializeCorrectiveAction(Uuid.CreateVersion4(), new NativeFactReference(
            new FindingAggregate(fixture.TenantId, fixture.ProgramId, findingId).Stream, Uuid.CreateVersion4(), 1),
            target.Stream, actionId, 1, 0, NativeFacts.Ref(), findingId, actionId);
        var handler = new MaterializeCorrectiveActionHandler(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateExecutor>(), new UnreadableContent(unavailable), TimeProvider.System, services.GetRequiredService<IDomainEventReader>());

        // Act
        var result = await handler.HandleAsync(new RequestContext<MaterializeCorrectiveAction>(command,
            RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.False((await fixture.ActionAsync(findingId, actionId)).IsMaterialized);
    }

    sealed class UnreadableContent(bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw new InvalidOperationException("No content can be written from an unverified admission.");
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The admission is temporarily absent.")
                : new ImmutableContentIntegrityException("The admission has a different digest."));
    }
}
