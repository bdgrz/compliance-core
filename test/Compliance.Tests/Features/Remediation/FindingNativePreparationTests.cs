using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingNativePreparationTests
{
    [Fact]
    public async Task ShouldAdmitActionOnRetryGivenPriorPreparationContentWasWrittenBeforeAdmissionFailed()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = new LostPreparation(fixture.Content);
        var clock = new MovingClock();
        var request = new AddCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            finding.FindingId, finding.Revision, "Remove stale access.", fixture.OwnerMemberId, fixture.Today.AddDays(7));
        var handler = new AddCorrectiveActionHandler(new NativeOperationExecutor(reader, executor, content), reader,
            content, clock);
        var context = new RequestContext<AddCorrectiveAction>(request, ProgramManagementServices.Actor(fixture.LeadUserId));
        await Assert.ThrowsAsync<IOException>(async () => await handler.HandleAsync(context, CancellationToken.None));
        Assert.Equal(finding.Revision, (await fixture.FindingStreamAsync(finding.FindingId)).Revision);
        clock.Now = clock.Now.AddDays(1);

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(request.ActionId, retry.Value!.OperationId);
        Assert.Equal(clock.Now, retry.Value.AcceptedAt);
        Assert.Equal(finding.Revision + 1, after.Revision);
        Assert.Equal(1, content.Interruptions);
    }

    sealed class LostPreparation(IImmutableContentStore inner) : IImmutableContentStore
    {
        bool _armed = true;
        public int Interruptions { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            var reference = await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
            if (_armed && schema == FindingContent.AcknowledgmentSchema)
            {
                _armed = false;
                Interruptions++;
                throw new IOException("The connection was lost after preparation bytes were stored, before native admission.");
            }
            return reference;
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class MovingClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
