using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
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

public sealed class CorrectiveActionAcceptedMaterializationReplayTests
{
    [Fact]
    public async Task ShouldReturnOriginalNativeMaterializationGivenAcceptedReceiverContentTemporarilyUnavailable()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(7));
        var action = await fixture.ActionAsync(finding.FindingId, Assert.Single(finding.CorrectiveActions).ActionId);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        CorrectiveActionAdmitted? admission = null;
        var parent = new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId);
        await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(parent.Stream, 0))
            if (record.Event is CorrectiveActionAdmitted candidate)
                admission = candidate;
        Assert.NotNull(admission);
        var command = new MaterializeCorrectiveAction(action.Added!.Operation.LogicalId,
            NativeFactReference.Of(parent.Stream, admission), action.Stream, admission.Operation.RootOperationId,
            admission.Generation, null, admission.Intent, finding.FindingId, action.ActionId);
        var content = new UnavailableContent();
        var handler = ActivatorUtilities.CreateInstance<MaterializeCorrectiveActionHandler>(services, content);

        // Act
        var replay = await handler.HandleAsync(new RequestContext<MaterializeCorrectiveAction>(command,
            RequestActor.System), CancellationToken.None);
        var after = await fixture.ActionAsync(finding.FindingId, action.ActionId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(NativeFactReference.Of(action.Stream, action.Added), Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(action.Version, after.Version);
        Assert.Equal(0, content.Reads);
    }

    sealed class UnavailableContent : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw new InvalidOperationException("An accepted native materialization never freezes a replacement.");
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            Reads++;
            return Task.FromException<ReadOnlyMemory<byte>>(new ImmutableContentUnavailableException("The original admitted content is temporarily unavailable."));
        }
    }
}
