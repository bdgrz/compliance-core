using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

public sealed class PersonalControlEvaluationTransportTests
{
    [Theory]
    [InlineData("submit", "direct")]
    [InlineData("submit", "mcp")]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("submit", "http")]
    [InlineData("review", "http")]
    public async Task ShouldEnforcePersonalTransportGivenValidControlEvaluationDecision(string operation, string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var evaluation = await ControlEvaluationTests.RecordAllAsync(source, await ControlEvaluationTests.StartAsync(source));
        if (operation == "review")
            evaluation = await source.AsAsync(source.OwnerUserId, ControlEvaluationTests.Submit(source, evaluation));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await NativeFacts.CountAsync(provider, source.TenantId, ControlEvaluation.Area);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = operation == "submit" ? source.OwnerUserId : source.ApproverUserId;
        var context = PersonalReadinessClosureTransportTests.Context(actor, transport);

        // Act
        var result = operation == "submit"
            ? await bus.DispatchAsync(ControlEvaluationTests.Submit(source, evaluation), context, CancellationToken.None)
            : await bus.DispatchAsync(ControlEvaluationTests.Review(source, evaluation, "accepted"), context, CancellationToken.None);
        var after = await NativeFacts.CountAsync(provider, source.TenantId, ControlEvaluation.Area);

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(evaluation.Revision + 1, result.Value.Revision);
            Assert.Equal(source.Member(actor), operation == "submit" ? result.Value.EvaluatorMemberId : result.Value.LatestReview!.ReviewerMemberId);
            Assert.True(after > before);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before, after);
        }
    }
    [Theory]
    [InlineData("evaluator", "Only the evaluator may record this evaluation.")]
    [InlineData("manager", "Only a Compliance Lead or Org Admin for the program may review an evaluation.")]
    [InlineData("self", "The evaluator cannot review their own evaluation without an approved, active separation-of-duties waiver.")]
    public async Task ShouldPreserveEvaluationAuthorityGivenNativeHttpConflictingActor(string refusal, string message)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var evaluator = refusal == "self" ? source.LeadUserId : source.OwnerUserId;
        var evaluation = await ControlEvaluationTests.RecordAllAsync(source, await ControlEvaluationTests.StartAsync(source, evaluator), evaluator);
        if (refusal != "evaluator")
            evaluation = await source.AsAsync(evaluator, ControlEvaluationTests.Submit(source, evaluation));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await NativeFacts.CountAsync(provider, source.TenantId, ControlEvaluation.Area);

        // Act
        var result = refusal == "evaluator"
            ? await HttpAsync(provider, source.OutsiderUserId, ControlEvaluationTests.Submit(source, evaluation), RequestErrorKind.Forbidden)
            : await HttpAsync(provider, evaluator, ControlEvaluationTests.Review(source, evaluation, "accepted"), RequestErrorKind.Forbidden);
        var after = await NativeFacts.CountAsync(provider, source.TenantId, ControlEvaluation.Area);

        // Assert
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before, after);
    }

    internal static Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid actor,
        IRequest<T> request, RequestErrorKind? expectedFailure = null) =>
        PersonalOccurrenceProofTransportTests.HttpAsync(provider, actor, request, expectedFailure);

}
