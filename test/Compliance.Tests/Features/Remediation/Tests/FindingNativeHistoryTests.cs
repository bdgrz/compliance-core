using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
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
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingNativeHistoryTests
{
    [Fact]
    public void ShouldKeepBoundedCommandAuthorityGivenManyCanceledClosureAttempts()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var source = new FindingAggregate(tenant, program, id, actionId);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var terms = new FindingRaiseTerms(new FindingSource("manual", null, null, "Original source text."),
            "Retained access", "Access survived offboarding.", "high", "VPN", Uuid.CreateVersion4(),
            new DateOnly(2026, 10, 15), actor, NativeFacts.Now, "manual", null);
        Assert.True(source.Raise(Root(id, nameof(RaiseFinding), null), terms,
            NativeFacts.Ref(FindingContent.LinksSchema), NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        Assert.True(source.AdmitAction(Root(actionId, nameof(AddCorrectiveAction), 1),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        for (var index = 0; index < 128; index++)
        {
            var root = Uuid.CreateVersion4();
            var operation = Root(root, nameof(CloseFinding), source.Revision);
            Assert.True(source.AcceptClosure(operation, NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref()).IsSuccess);
            var child = operation with
            {
                LogicalId = Uuid.CreateVersion4(),
                CommandKind = nameof(AbortFindingClosure),
                CausationEventId = source.PendingClosure!.Metadata.EventId,
            };
            Assert.True(source.CancelClosure(child, NativeFacts.Ref(FindingContent.FailureSchema)).IsSuccess);
        }
        var facts = new AggregateScenario<FindingAggregate>(source).PendingEvents.ToArray();

        // Act
        var owner = NativeFacts.Replay(new FindingAggregate(tenant, program, id), facts);
        var retained = typeof(FindingAggregate).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(258, owner.Revision);
        Assert.Equal(1, owner.Generation);
        Assert.Null(owner.PendingClosure);
        Assert.Null(owner.Closed);
        Assert.Equal("Original source text.", owner.Raised!.Source.SourceText);
        Assert.Equal((ulong)facts.Length, owner.Version);

        OperationProvenance Root(Uuid logical, string kind, long? expected) => FindingContent.Provenance(logical,
            kind, source.Stream, actor, expected is null ? null : (ulong)expected.Value, new string('a', 64),
            NativeFacts.Now) with
        { FirstOwner = source.Stream };
    }
}
