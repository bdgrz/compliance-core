using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Handlers;
using Bdgrz.Compliance.Tests.Testing;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed partial class SystemDescriptionDraftCommandHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectWellHashedSnapshotProofGivenNoncanonicalClaimedOrResolvedSourceStream(bool forgeClaim)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var resolved = await fixture.Sources.ResolveAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        var proof = await fixture.PeriodContent.ReadJsonAsync<SystemDescriptionSnapshotSourceProof>(fixture.TenantId.ToString(), resolved.Value.Proof);
        var canonical = proof.Facts[0].Fact.Stream;
        var foreign = new EventStreamAddress(fixture.TenantId.ToString(), "another-owner", canonical.Resource);
        if (forgeClaim)
            proof = proof with { Facts = [proof.Facts[0] with { Fact = proof.Facts[0].Fact with { Stream = foreign } }] };
        var reference = await fixture.PeriodContent.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(), SystemDescriptionSnapshotSources.Schema, proof);
        var verifier = new SystemDescriptionSnapshotSources(new ForeignStreamReader(fixture.PeriodEvents, canonical, foreign), fixture.PeriodContent);

        // Act
        var failure = await Record.ExceptionAsync(() => verifier.VerifyAsync(fixture.TenantId, fixture.ProgramId, proof.Scope, proof.Sources, reference, CancellationToken.None).AsTask());

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRejectSnapshotProofGivenMissingNativeEventIdentityOrVersion(bool missingEvent)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var resolved = await fixture.Sources.ResolveAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId, null, CancellationToken.None);
        var proof = await fixture.PeriodContent.ReadJsonAsync<SystemDescriptionSnapshotSourceProof>(fixture.TenantId.ToString(), resolved.Value.Proof);
        var source = proof.Facts[0];
        proof = proof with { Facts = [source with { Fact = missingEvent ? source.Fact with { EventId = Uuid.Empty } : source.Fact with { AggregateVersion = 0 } }] };
        var reference = await fixture.PeriodContent.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(), SystemDescriptionSnapshotSources.Schema, proof);
        var verifier = new SystemDescriptionSnapshotSources(fixture.PeriodEvents, fixture.PeriodContent);

        // Act
        var failure = await Record.ExceptionAsync(() => verifier.VerifyAsync(fixture.TenantId, fixture.ProgramId, proof.Scope, proof.Sources, reference, CancellationToken.None).AsTask());

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldKeepExactHistoricalSnapshotQualificationGivenLaterExaminationBaselineAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var created = await fixture.CreateHandler().HandleAsync(fixture.Context(new CreateSystemDescriptionDraft(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, fixture.ExaminationId, "Historical narrative", Sections())), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);
        var source = await fixture.Descriptions.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.DescriptionId);
        var proof = source.Owner.SourceSnapshotContent;
        var engagement = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);
        var replacement = new TypeIBaselineSourceReference("approved_boundary", fixture.BoundaryId, Uuid.CreateVersion4(), 3, new string('b', 64));
        var preview = Bdgrz.Compliance.Tests.Features.Examinations.TypeIBaselinePreviewTestFactory.Create(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, [replacement]);
        Assert.Null(engagement.Rules.AmendBaseline(engagement.Owner.Revision, Uuid.CreateVersion4(), preview, [fixture.BoundaryId], [], "Updated baseline source",
            fixture.UserMemberId, "Baseline author", Now.AddMinutes(5)));
        await ExaminationSeeds.WriteAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.PeriodContent, engagement.Rules);
        var review = new ReviewSystemDescription(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.DescriptionId, 1, "approve", "Reviewed historical exact snapshot", 1);
        var reviewed = await new ReviewSystemDescriptionHandler(fixture.Operations).HandleAsync(FixtureMemberContext(review, fixture.ReviewerId), CancellationToken.None);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);

        // Act
        var approved = await new ApproveSystemDescriptionHandler(fixture.Operations).HandleAsync(FixtureMemberContext(new ApproveSystemDescription(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.DescriptionId, 1, "Approve exact historical snapshot", 2), fixture.ApproverId), CancellationToken.None);
        var current = await fixture.Descriptions.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.DescriptionId);

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.Equal("approved", current.CurrentVersion!.Status);
        Assert.Equal(fixture.BaselineId, current.CurrentVersion.Scope.TypeIBaselineId);
        Assert.Equal(proof, current.Owner.SourceSnapshotContent);
        Assert.Equal(fixture.BaselineSourceVersionId, Assert.Single(current.CurrentVersion.Content.Sources).VersionId);
    }

    static Cntryl.Portia.RequestContext<T> FixtureMemberContext<T>(T request, Uuid user) where T : IRequestBase => new(request,
        new RequestDispatchContext(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim("iss","bdgrz"),new System.Security.Claims.Claim("sub",user.ToString()),
            new System.Security.Claims.Claim("name","Independent member")], "UnitTest")), new HttpInvocation("PUT", "/description", "/description", "snapshot-proof-test")));
    sealed class ForeignStreamReader(IDomainEventReader inner, EventStreamAddress canonical, EventStreamAddress foreign) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset, CancellationToken ct) => Rewrite(inner.ReadAsync(canonical, fromOffset, ct), ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct) => Rewrite(inner.ReadAsync(
            EventStreamPattern.ForPattern(canonical.Realm, canonical.Area, canonical.Resource), cursor, ct), ct);
        async IAsyncEnumerable<DomainEventRecord> Rewrite(IAsyncEnumerable<DomainEventRecord> records, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in records.WithCancellation(ct))
                yield return record with { Stream = foreign };
        }
    }
}
