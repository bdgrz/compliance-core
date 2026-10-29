using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorResolutionReplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayExactCanceledResultGivenFailureContentUnavailable(bool corrupt)
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(id, responseId)).IsSuccess);
        var admission = await fixture.ResolveAsync(id);
        Assert.True(admission.Result.IsSuccess, admission.Result.Error?.Message);
        var owner = await fixture.RequestAsync(id);
        var pending = owner.PendingResolution!;
        var failure = await fixture.Content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            AuditorContent.FailureSchema, new AuditorRequestFailureProof(fixture.ProgramId, id,
                "The exact selected response cannot be materialized.", responseId, admission.CommandId,
                pending.Generation, NativeFactReference.Of(owner.Stream, pending), pending.Intent));
        var command = new AbortAuditorRequestResolution(Uuid.CreateVersion4(), admission.CommandId, owner.Stream,
            pending.Generation, 0, failure);
        var context = new Cntryl.Portia.RequestContext<AbortAuditorRequestResolution>(command,
            new RequestDispatchContext(RequestActor.System));
        var first = await fixture.Handler<AbortAuditorRequestResolutionHandler>().HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var canceled = await fixture.RequestAsync(id);
        var store = new UnreadableStore(fixture.Content, corrupt);
        var handler = ActivatorUtilities.CreateInstance<AbortAuditorRequestResolutionHandler>(fixture.Services, store);

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var changed = await handler.HandleAsync(new Cntryl.Portia.RequestContext<AbortAuditorRequestResolution>(command with
        { FailureProof = failure with { Sha256 = new string('f', 64) } }, new RequestDispatchContext(RequestActor.System)),
            CancellationToken.None);
        var saved = await fixture.RequestAsync(id);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(((ReceiverOutcome.Applied)first.Value!).Fact, ((ReceiverOutcome.Replayed)replay.Value!).Fact);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.False(changed.Error!.IsTransient);
        Assert.Equal(canceled.CommittedStreamPosition, saved.CommittedStreamPosition);
        Assert.True(saved.IsOpen);
        Assert.Null(saved.PendingResolution);
        Assert.Equal(0, store.Reads);
    }

    sealed class UnreadableStore(IImmutableContentStore inner, bool corrupt) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            Reads++;
            throw corrupt ? new ImmutableContentIntegrityException("The original failure proof is corrupt.")
                : new ImmutableContentUnavailableException("The original failure proof is temporarily missing.");
        }
    }
}
