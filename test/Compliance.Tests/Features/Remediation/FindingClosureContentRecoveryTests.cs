using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingClosureContentRecoveryTests
{
    [Theory]
    [InlineData(ClosureIntent.Schema)]
    [InlineData(FindingContent.DecisionSchema)]
    [InlineData(FindingContent.AdmittedChildSchema)]
    [InlineData(ChildAdmissionIntent.Schema)]
    [InlineData(FindingContent.ActionAuthoredSchema)]
    [InlineData(FindingContent.ActionFactsSchema)]
    public async Task ShouldResumeClosureAcquisitionGivenFrozenContentTemporarilyUnavailable(string schema)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, close);
        var record = await fixture.StartClosureAsync(finding.FindingId, close.CommandId);
        var request = Assert.IsType<AuthorizeCorrectiveActionFindingClosureUse>(await fixture.ClosureStepAsync(record,
            "acquire", nameof(AuthorizeCorrectiveActionFindingClosureUse), Assert.Single(record.Targets)));
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = new WithheldContent(fixture.Content, schema);
        var handler = new AuthorizeCorrectiveActionFindingClosureUseHandler(reader,
            services.GetRequiredService<IAggregateExecutor>(), content, TimeProvider.System,
            new FindingClosureVerifier(reader, content, services.GetRequiredService<IDomainEventReader>()), services.GetRequiredService<FindingClosureFences>());
        var before = await fixture.ActionAsync(finding.FindingId, request.SourceOwnerId);
        var context = new Cntryl.Portia.RequestContext<AuthorizeCorrectiveActionFindingClosureUse>(request,
            new RequestDispatchContext(RequestActor.System));

        // Act
        var waiting = await handler.HandleAsync(context, CancellationToken.None);
        var pending = await fixture.FindingStreamAsync(finding.FindingId, close.CommandId);
        var heldWhileUnavailable = await fixture.ActionAsync(finding.FindingId, request.SourceOwnerId);
        content.Withhold = false;
        var resumed = await handler.HandleAsync(context, CancellationToken.None);
        var completed = await fixture.RunClosureAsync(finding.FindingId, close.CommandId);

        // Assert
        Assert.True(content.Intercepted);
        Assert.False(waiting.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, waiting.Error!.Kind);
        Assert.True(waiting.Error.IsTransient);
        Assert.NotNull(pending.PendingClosure);
        Assert.Null(pending.TerminalClosure(close.CommandId));
        Assert.Equal(before.Version, heldWhileUnavailable.Version);
        Assert.False(heldWhileUnavailable.Uses.HasActiveUse);
        Assert.True(resumed.IsSuccess, resumed.Error?.Message);
        Assert.IsType<ReceiverOutcome.Applied>(resumed.Value);
        Assert.Equal(ProcessState.Completed, completed.State);
        Assert.Equal("closed", (await fixture.GetFindingAsync(finding.FindingId)).Status);
    }

    sealed class WithheldContent(IImmutableContentStore inner, string schema) : IImmutableContentStore
    {
        public bool Withhold { get; set; } = true;
        public bool Intercepted { get; private set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string contentSchema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, contentSchema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (!Withhold || reference.Schema != schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            Intercepted = true;
            return Task.FromException<ReadOnlyMemory<byte>>(
                new ImmutableContentUnavailableException("The selected content is temporarily unreadable."));
        }
    }
}
