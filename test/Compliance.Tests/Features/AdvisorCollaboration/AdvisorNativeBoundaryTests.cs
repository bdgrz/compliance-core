using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorNativeBoundaryTests
{
    [Fact]
    public async Task ShouldRetainOnlyCurrentAuthorityGivenManyFeedbackAdmissionsAndResponses()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, source) = await host.SeedFeedbackAsync();

        // Act
        for (var index = 0; index < 105; index++)
        {
            if (index > 0)
                Assert.True((await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source,
                    2 + index))).IsSuccess);
            Assert.True((await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
                Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId,
                "Client response " + index, index + 1)), CancellationToken.None)).IsSuccess);
        }
        var handoff = await host.HandoffAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);
        var retained = new Aggregate[] { handoff.Handoff, thread.Owner }.SelectMany(owner => owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 2 })).ToArray();

        // Assert
        Assert.Equal(107, handoff.Revision);
        Assert.Equal(106, thread.Revision);
        Assert.Empty(retained);
        Assert.Equal(105, handoff.Admitted.Count);
        Assert.Equal(105, thread.Responses.Count);
        Assert.Equal("Client response 0", thread.Responses[0].Response);
        Assert.Equal("Client response 104", thread.Responses[^1].Response);
        Assert.Equal("Please clarify the control owner.", thread.Recorded!.Body);
    }

    [Fact]
    public async Task ShouldBoundNativeCorrectionGivenLargeValidCompleteText()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        var text = new string('界', 8000);
        var correction = Uuid.CreateVersion4();

        // Act
        var result = await host.CorrectHandler.HandleAsync(host.Http(new CorrectAdvisorFeedback(host.TenantId,
            host.ProgramId, handoff, feedback, correction, text, text, 1)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoff, feedback);
        var fact = Assert.Single(await host.EventsAsync<AdvisorFeedbackCorrected>(thread.Stream));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(fact, ComplianceCoreJsonContext.Default.AdvisorFeedbackCorrected);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(bytes.Length <= 48 * 1024, $"The complete valid correction produced a {bytes.Length}-byte native payload.");
        Assert.Equal(text, Assert.Single(thread.Corrections).CorrectedBody);
        Assert.Equal(text, Assert.Single(thread.Corrections).CorrectionReason);
        Assert.Equal("Please clarify the control owner.", thread.Recorded!.Body);
    }
}
