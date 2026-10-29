using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Independence.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
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
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskAcceptanceIdentityTests
{
    [Theory]
    [InlineData("rationale")]
    [InlineData("expiry")]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("waiver")]
    public async Task ShouldBindAllOriginalAcceptanceFieldsGivenLaterResidualAssessment(string change)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var assessor = Uuid.CreateVersion4();
        var approverUser = Uuid.CreateVersion4();
        var approver = RbacIds.Member(tenant, approverUser);
        var residual = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMonths(6);
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(approver, "Author"), now));
        var owner = new RiskEvaluationRules(tenant, risk, command);
        Assert.Null(owner.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Inherent", assessor, "Assessor", now));
        Assert.Null(owner.ChooseTreatment(program, 1, "accept", "Accept within tolerance", assessor, "Author", now));
        Assert.Null(owner.RecordAssessment(program, 2, residual, method.Current!, "residual", 2, 2,
            "Residual", assessor, "Assessor", now));
        var request = new AcceptRisk(command, tenant, program, risk, 3, residual, "compliance_lead", expires, "Original acceptance");
        Assert.Null(owner.Accept(program, 3, command, residual, method.Current!, "compliance_lead", true,
            expires, request.Rationale, approver, "Original approver", now));
        Assert.Null(owner.RecordAssessment(program, 4, Uuid.CreateVersion4(), method.Current!, "residual", 1, 1,
            "Later residual", assessor, "Assessor", now));
        await using var source = new RiskEvaluationSourceFixture();
        await source.SeedAsync(owner);
        var facts = await source.FactsAsync(tenant, risk);
        var hydrated = await source.Evaluations.ReadAsync(tenant, program, risk);
        var permissions = new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true));
        var governance = new RiskGovernanceReader(source.Reader, source.Events, source.Content);
        var waivers = new SeparationOfDutiesWaiverProofResolver(source.Reader, new EventStoreSourceFactResolver(source.Events));

        // Act
        var original = await source.Operations().AcceptAsync(RiskEvaluationSourceFixture.Context(request, approverUser), permissions, governance, waivers, CancellationToken.None);
        var changed = await source.Operations().AcceptAsync(RiskEvaluationSourceFixture.Context(request with
        {
            Rationale = change == "rationale" ? "Changed acceptance" : request.Rationale,
            ExpiresAt = change == "expiry" ? expires.AddDays(1) : expires,
            ExpectedRevision = change == "revision" ? 4 : request.ExpectedRevision,
            SeparationOfDutiesWaiverId = change == "waiver" ? Uuid.CreateVersion4() : null,
        }, change == "actor" ? Uuid.CreateVersion4() : approverUser), permissions, governance, waivers, CancellationToken.None);
        var result = Assert.IsType<Result<RiskAcceptanceView>>(original);
        var conflict = Assert.IsType<Result<RiskAcceptanceView>>(changed);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(residual, result.Value.ResidualAssessmentId);
        Assert.Equal("Original approver", result.Value.Approver.Display);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error?.Kind);
        Assert.Equal(5, hydrated.Revision);
        Assert.Equal(facts.Count, (await source.FactsAsync(tenant, risk)).Count);
    }
}
