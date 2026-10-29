using Bdgrz.Compliance.Tests.Testing;
using System.Runtime.CompilerServices;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Handlers;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

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
        var preview = Bdgrz.Compliance.Tests.Features.Examinations.Tests.TypeIBaselinePreviewTestFactory.Create(fixture.TenantId, fixture.ProgramId,
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
