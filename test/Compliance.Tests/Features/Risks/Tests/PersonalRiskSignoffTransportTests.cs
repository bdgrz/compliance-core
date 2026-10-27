using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class PersonalRiskSignoffTransportTests
{
    [Theory]
    [InlineData("treatment", "direct")]
    [InlineData("treatment", "mcp")]
    [InlineData("submit", "direct")]
    [InlineData("submit", "mcp")]
    [InlineData("completion", "direct")]
    [InlineData("completion", "mcp")]
    public async Task ShouldRefusePersonalRiskSignoffGivenNonHttpInvocation(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(before.Revision, retained.Revision);
    }

    [Theory]
    [InlineData("treatment")]
    [InlineData("submit")]
    [InlineData("completion")]
    public async Task ShouldRetainAttributedRiskSignoffGivenNativeHttp(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http");
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var expectedFacts = action == "completion" ? 2UL : 1UL;
        Assert.Equal(before.CommittedStreamPosition + expectedFacts, retained.CommittedStreamPosition);
        Assert.Equal(before.Revision + (long)expectedFacts, retained.Revision);
        var view = retained.View;
        var expected = RbacIds.Member(fixture.Source.TenantId, fixture.Actor).ToString();
        if (action == "treatment")
        {
            var treatment = Assert.Single(view.ControlTreatments);
            Assert.Equal(expected, treatment.ReviewedBy!.Id);
            Assert.Equal("accepted", treatment.Status);
            Assert.Equal(fixture.Source.ControlVersionId, treatment.ControlVersionId);
        }
        else
        {
            var treatment = Assert.Single(view.TreatmentActions);
            var completion = Assert.Single(treatment.Completions);
            Assert.Equal([fixture.Evidence], completion.EvidenceRequestIds);
            if (action == "submit")
            {
                Assert.Equal(expected, completion.SubmittedBy.Id);
                Assert.Null(completion.ReviewOutcome);
                Assert.Equal("completion_submitted", treatment.Status);
            }
            else
            {
                Assert.Equal(expected, completion.ReviewedBy!.Id);
                Assert.Equal("accept", completion.ReviewOutcome);
                Assert.Equal("completed", treatment.Status);
            }
        }
    }

    [Theory]
    [InlineData("treatment", "proposer", "A control treatment proposer cannot review their own assertion.")]
    [InlineData("completion", "submitter", "Only the assigned reviewer may review this completion.")]
    [InlineData("completion", "owner", "Only the assigned reviewer may review this completion.")]
    [InlineData("completion", "submitter_missing_waiver", "The separation-of-duties waiver is not active for this member and completion.")]
    [InlineData("completion", "owner_missing_waiver", "The separation-of-duties waiver is not active for this member and completion.")]
    [InlineData("completion", "unassigned", "Only the assigned reviewer may review this completion.")]
    public async Task ShouldPreserveRiskParticipantSeparationGivenNativeHttp(string action, string conflict, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        await fixture.UseConflictingActorAsync(conflict);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http");
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
    }

    internal static async Task<Result> HttpAsync(IServiceProvider provider, Uuid user, IRequest request,
        RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user), Invocation("http")), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    internal static async Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid user, IRequest<T> request,
        RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user), Invocation("http")), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        if (result.IsSuccess && request is ReviewRiskTreatmentActionCompletion review)
        {
            var process = await RiskActionProcesses.RunAsync(provider, review.TenantId, review.CommandId);
            Assert.Equal(ProcessState.Completed, process.State);
        }
        return result;
    }

    static RequestInvocation Invocation(string transport) => transport switch
    {
        "mcp" => new McpInvocation("synthetic.risk.signoff"),
        "direct" => new DirectInvocation(),
        _ => new HttpInvocation("POST", "/synthetic/risk/signoff", "/synthetic/risk/signoff", "synthetic")
    };

    sealed class Fixture(RiskGovernanceHandlerTests.Fixture source, ServiceProvider provider, string actionKind) : IAsyncDisposable
    {
        public RiskGovernanceHandlerTests.Fixture Source { get; } = source;
        Uuid _id;
        Uuid _evidence;
        Uuid _worker;
        Uuid? _missingWaiver;
        public Uuid Actor { get; private set; }
        public Uuid Evidence => _evidence;

        public static async Task<Fixture> CreateAsync(string action)
        {
            var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
            var fixture = new Fixture(source, await AttestRiskManagementWriteWallTests.ComposeAsync(source), action)
            {
                Actor = action == "submit" ? source.AssessorUserId : source.ApproverUserId
            };
            if (action == "treatment")
            {
                var proposal = await source.Scenario(source.AssessorUserId).When(new ProposeRiskControlTreatment(Uuid.CreateVersion4(),
                    source.TenantId, source.ProgramId, source.RiskId, 0, source.ControlId, source.ControlVersionId,
                    "Current approved control mitigates risk.")).ExpectSuccess();
                fixture._id = proposal.Value.TreatmentId;
            }
            else
            {
                var worker = await source.SeedMemberAsync();
                fixture._worker = worker.UserId;
                fixture._evidence = await source.SeedEvidenceAsync(true, worker);
                var added = await source.Scenario(source.AssessorUserId)
                    .When(source.AddAction(worker, [fixture._evidence], 0)).ExpectSuccess();
                fixture._id = added.Value.ActionId;
                if (action == "completion")
                {
                    var submission = await HttpAsync(source.Provider, source.AssessorUserId,
                        source.Submit(fixture._id, 1, [fixture._evidence]));
                    await source.AssignReviewAsync(submission.Value.SubmissionId);
                }
            }
            return fixture;
        }

        public Task UseConflictingActorAsync(string conflict)
        {
            Actor = conflict.StartsWith("owner", StringComparison.Ordinal) ? _worker : Source.AssessorUserId;
            if (conflict.EndsWith("missing_waiver", StringComparison.Ordinal))
                _missingWaiver = Uuid.CreateVersion4();
            return Task.CompletedTask;
        }

        public async Task<Result> DecideAsync(string action, string transport)
        {
            await using var scope = provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            var context = new RequestDispatchContext(ProgramManagementServices.Actor(Actor), Invocation(transport));
            if (action == "submit")
            {
                var result = await bus.DispatchAsync(Source.Submit(_id, 1, [_evidence]), context, CancellationToken.None);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error);
            }
            if (action == "treatment")
                return await bus.DispatchAsync(Source.ReviewTreatment(_id, "accept"), context, CancellationToken.None);
            var review = Source.ReviewAction(_id, 2, "accept") with { SeparationOfDutiesWaiverId = _missingWaiver };
            var reviewed = await bus.DispatchAsync(review, context, CancellationToken.None);
            if (!reviewed.IsSuccess)
                return Result.Failure(reviewed.Error);
            var process = await RiskActionProcesses.RunAsync(provider, Source.TenantId, review.CommandId);
            Assert.Equal(ProcessState.Completed, process.State);
            return Result.Success;
        }

        public async Task<SignoffSnapshot> ReadAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            var snapshot = await scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>()
                .ReadRiskAsync(Source.TenantId, Source.ProgramId, Source.RiskId);
            var view = snapshot.View(Source.RiskId, [], DateOnly.FromDateTime(DateTime.UtcNow));
            if (actionKind == "treatment")
            {
                var line = await reader.HydrateAsync(new RiskTreatmentAssertion(Source.TenantId, Source.ProgramId,
                    Source.RiskId, Source.ControlId));
                return new SignoffSnapshot(line.CommittedStreamPosition, line.Revision, view);
            }
            var action = await reader.HydrateAsync(new RiskTreatmentAction(Source.TenantId, Source.ProgramId,
                Source.RiskId, _id));
            return new SignoffSnapshot(action.CommittedStreamPosition, action.Revision, view);
        }

        public sealed record SignoffSnapshot(ulong CommittedStreamPosition, long Revision, RiskGovernanceView View);

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await Source.Provider.DisposeAsync();
        }
    }
}
