using System.Collections;
using System.Reflection;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.SourceUse.Tests;

public sealed class SourceUseReservationsNativeBoundaryTests
{
    [Fact]
    public void ShouldRetainOnlyLiveAndSelectedSlotsGivenLongSettledReservationHistory()
    {
        // Arrange
        var owner = new FirmStaffRecord(Uuid.CreateVersion4()).Stream;
        var uses = new SourceUseReservations(owner);

        // Act
        for (var index = 0; index < 128; index++)
        {
            var operation = new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null,
                "Native boundary fixture", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator"),
                owner, "initial", 0, new string('a', 64), DateTimeOffset.UtcNow, owner, owner);
            var intent = new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 2,
                "application/json", SourceUseAuthorizationIntent.Schema);
            uses.ApplyGranted(SourceUsePurpose.ProfessionalDutyRecording,
                new FirmStaffProfessionalDutyUseAuthorized(owner, 1, 1, intent, null, operation, null, null));
            uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording,
                new FirmStaffProfessionalDutyUseSettled(owner, 1, 1, intent, null,
                    operation with { LogicalId = Uuid.CreateVersion4() }, null, null));
        }
        var retained = typeof(SourceUseReservations).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(uses) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Equal(128UL, uses.Generation);
        Assert.False(uses.HasActiveUse);
        Assert.Empty(uses.ActiveUses);
        Assert.Empty(retained);
    }
}
