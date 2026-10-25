using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SourceUse;

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
