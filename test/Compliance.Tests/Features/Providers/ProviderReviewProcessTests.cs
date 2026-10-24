using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderReviewProcessTests
{
    [Fact]
    public async Task ShouldRecordReviewGivenAdmittedReviewAndHeldReportRevision()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();

        // Act
        var admitted = await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)));
        var pending = await host.ReviewAsync(reviewId);
        var process = await host.DriveReviewAsync(reviewId);
        var review = await host.ReviewAsync(reviewId);
        var reportFacts = await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, report.ReportId));

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(reviewId, admitted.Value.OperationId);
        Assert.True(pending.IsPending);
        Assert.Equal(ProcessState.Completed, process.State);
        var recorded = Assert.IsType<ProviderReviewRecorded>(review.Recorded);
        Assert.Equal(1, recorded.ProviderRevision);
        Assert.Equal(1, recorded.AssuranceReportRevision);
        Assert.Equal("material", recorded.ProviderMateriality);
        Assert.Equal(RbacIds.Member(host.Tenant, host.User).ToString(), recorded.Actor.Id);
        Assert.Equal(review.Requested!.Intent, recorded.Intent);
        var registration = await host.Content.ReadJsonAsync<ProviderReviewRegistration>(host.Tenant.ToString(), recorded.OperationResult!);
        Assert.Equal(reviewId, registration.ReviewId);
        Assert.IsType<ProviderAssuranceReportProviderReviewUseAuthorized>(reportFacts[1]);
        Assert.IsType<ProviderAssuranceReportProviderReviewUseSettled>(reportFacts[2]);
        Assert.False((await host.ReportAsync(report.ReportId)).Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        var request = new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId));
        var first = await host.SendAsync(request);
        await host.DriveReviewAsync(reviewId);
        await host.ReviseReportAsync(report.ReportId, 1);

        // Act
        var retry = await host.SendAsync(request);

        // Assert
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(2, (await host.FactsAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId))).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        var request = new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId));
        Assert.True((await host.SendAsync(request)).IsSuccess);

        // Act
        var changedIntent = await host.SendAsync(request with { Content = request.Content with { Rationale = "Different." } });
        var changedActor = await host.SendAsync(request, user: Uuid.CreateVersion4());
        var changedProvider = await host.SendAsync(request with { ProviderId = Uuid.CreateVersion4() });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.True(changedProvider.Error?.Kind is RequestErrorKind.Conflict or RequestErrorKind.NotFound);
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId)));
    }

    [Fact]
    public async Task ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();

        // Act
        var noReview = await host.SendAsync(new RecordProviderReview(Uuid.Empty, host.Tenant, host.ProviderId, Review()));
        var noProvider = await host.SendAsync(new RecordProviderReview(Uuid.CreateVersion4(), host.Tenant, Uuid.Empty, Review()));
        var noEvidence = await host.SendAsync(new RecordProviderReview(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Review() with { Evidence = null }));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, noReview.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noProvider.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noEvidence.Error?.Kind);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldEnforcePersonalTransportGivenValidProviderReview(string transport)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var reviewId = Uuid.CreateVersion4();

        // Act
        var result = await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review()), transport);

        // Assert
        var facts = await host.FactsAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId));
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Single(facts);
            Assert.IsType<ProviderReviewRequested>(facts[0]);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Empty(facts);
        }
    }

    [Theory]
    [InlineData("grant", RequestErrorKind.Forbidden, "The actor requires organization-wide provider authority.")]
    [InlineData("provider", RequestErrorKind.NotFound, "The provider was not found.")]
    [InlineData("report", RequestErrorKind.NotFound, "The assurance report was not found for this provider.")]
    [InlineData("artifact", RequestErrorKind.NotFound, "A governed artifact reference was not found in its owning context.")]
    public async Task ShouldPreserveProviderSourceAuthorityGivenNativeHttpInvalidInput(string refusal, RequestErrorKind kind,
        string message)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        if (refusal == "grant")
            host.Permissions.Allowed = false;
        var content = refusal switch
        {
            "report" => Review(Uuid.CreateVersion4()),
            "artifact" => Review() with { Evidence = Citation(artifactId: Uuid.CreateVersion4()) },
            _ => Review(),
        };
        var reviewId = Uuid.CreateVersion4();

        // Act
        var result = await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant,
            refusal == "provider" ? Uuid.CreateVersion4() : host.ProviderId, content));

        // Assert
        Assert.Equal(kind, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Empty(await host.FactsAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId)));
    }

    [Theory]
    [InlineData("kind_mismatch")]
    [InlineData("before_period_end")]
    [InlineData("stale_report_acceptable")]
    [InlineData("future_review")]
    [InlineData("cadence_over_one_year")]
    [InlineData("next_not_after_review")]
    [InlineData("no_evidence")]
    [InlineData("exceptions_with_acceptable")]
    [InlineData("missing_exceptions")]
    [InlineData("unknown_conclusion")]
    public async Task ShouldRejectReviewGivenUnsupportedConclusion(string fault)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var stale = fault == "stale_report_acceptable";
        var report = await host.RecordReportAsync(content: stale
            ? Report() with { PeriodStart = new DateOnly(2024, 7, 1), PeriodEnd = new DateOnly(2025, 6, 30) }
            : Report());
        var good = Review(report.ReportId);
        var content = fault switch
        {
            "kind_mismatch" => good with { EvidenceKind = "iso27001" },
            "before_period_end" => good with { ReviewedAt = new DateOnly(2026, 6, 1), NextReviewDue = new DateOnly(2027, 6, 1) },
            "stale_report_acceptable" => good,
            "future_review" => good with { ReviewedAt = new DateOnly(2999, 10, 3), NextReviewDue = new DateOnly(2999, 12, 3) },
            "cadence_over_one_year" => good with { NextReviewDue = new DateOnly(2027, 9, 2) },
            "next_not_after_review" => good with { NextReviewDue = good.ReviewedAt },
            "no_evidence" => good with { AssuranceReportId = null, Evidence = null },
            "exceptions_with_acceptable" => good with { Exceptions = ["Unexpected"] },
            "missing_exceptions" => good with { Conclusion = "acceptable_with_exceptions" },
            _ => good with { Conclusion = "approved" },
        };
        var reviewId = Uuid.CreateVersion4();

        // Act
        var result = await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, content));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(await host.FactsAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId)));
    }

    [Fact]
    public async Task ShouldAllowStaleReportGivenNotAcceptableConclusion()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync(content: Report() with
        {
            PeriodStart = new DateOnly(2024, 7, 1),
            PeriodEnd = new DateOnly(2025, 6, 30),
        });

        // Act
        var result = await host.SendAsync(new RecordProviderReview(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Review(report.ReportId, "not_acceptable")));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldCancelReviewGivenReportRevisedAfterAdmission()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        await host.ReviseReportAsync(report.ReportId, 1);

        // Act
        var process = await host.DriveReviewAsync(reviewId);
        var review = await host.ReviewAsync(reviewId);
        var reportOwner = await host.ReportAsync(report.ReportId);
        var reportFacts = await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, report.ReportId));

        // Assert
        Assert.Equal(ProcessState.Aborted, process.State);
        Assert.Null(review.Recorded);
        var canceled = Assert.IsType<ProviderReviewCanceled>(review.Canceled);
        Assert.Equal(review.Requested!.Intent, canceled.Intent);
        Assert.Equal(2, reportOwner.Revision);
        Assert.False(reportOwner.Uses.HasActiveUse);
        Assert.IsType<ProviderAssuranceReportProviderReviewUseSettled>(reportFacts[^1]);
        Assert.DoesNotContain(reportFacts, fact => fact is ProviderAssuranceReportProviderReviewUseAuthorized);
        var failure = await host.Content.ReadJsonAsync<ProviderReviewFailureProof>(host.Tenant.ToString(), canceled.SourceProofs!);
        Assert.Equal(ProviderReviewFailureProof.SourceMismatch, failure.Reason);
    }

    [Fact]
    public async Task ShouldRecordReviewGivenExternalEvidenceWithoutHoldingAReport()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review()))).IsSuccess);

        // Act
        var process = await host.DriveReviewAsync(reviewId);
        var review = await host.ReviewAsync(reviewId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        var recorded = Assert.IsType<ProviderReviewRecorded>(review.Recorded);
        Assert.Null(recorded.AssuranceReportRevision);
        Assert.DoesNotContain(process.Facts.Keys, key => key.Contains("Authorize", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReplayWithoutDuplicateGrantGivenAcquireRetriedAfterCrash()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        var root = await host.RootAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId), reviewId);
        var record = await host.PrepareProcessAsync(await host.ReviewKey(reviewId), root, ((ProviderReviewRequested)root.Event).Intent);
        var acquire = await host.CommandAsync(record, nameof(AuthorizeProviderAssuranceReportProviderReviewUse), "acquire");
        var granted = await host.ReceiveAsync(acquire);

        // Act: the worker crashed after the receiver granted and before the process record advanced.
        var identical = await host.CommandAsync(record, nameof(AuthorizeProviderAssuranceReportProviderReviewUse), "acquire");
        var replay = await host.ReceiveAsync(identical);
        var resumed = await host.ResumeAsync(await host.ReviewKey(reviewId));

        // Assert
        Assert.Equal(acquire, identical);
        Assert.IsType<ReceiverOutcome.Applied>(granted.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
        Assert.Equal(ProcessState.Completed, resumed.State);
        var facts = await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, report.ReportId));
        Assert.Single(facts.OfType<ProviderAssuranceReportProviderReviewUseAuthorized>());
        Assert.Single(facts.OfType<ProviderAssuranceReportProviderReviewUseSettled>());
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        var root = await host.RootAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId), reviewId);
        var record = await host.PrepareProcessAsync(await host.ReviewKey(reviewId), root, ((ProviderReviewRequested)root.Event).Intent);
        var acquire = (AuthorizeProviderAssuranceReportProviderReviewUse)await host.CommandAsync(record,
            nameof(AuthorizeProviderAssuranceReportProviderReviewUse), "acquire");
        var noGrants = await host.Content.WriteJsonAsync(host.Tenant.ToString(), Uuid.CreateVersion4(),
            "provider-review-use-proofs.v1", Array.Empty<NativeFactReference>());
        var complete = new CompleteProviderAssuranceReview(Uuid.CreateVersion4(), record.Source,
            ProviderAssuranceStreams.Review(host.Tenant, reviewId), reviewId, 0, null, record.Intent, reviewId, noGrants);

        // Act
        var wrongSource = await host.ReceiveAsync(acquire with { Source = acquire.Source with { EventId = Uuid.CreateVersion4() } });
        var wrongRevision = await host.ReceiveAsync(acquire with { SourceRevision = 7 });
        var wrongEvent = await host.ReceiveAsync(acquire with { SourceContentEventId = Uuid.CreateVersion4() });
        var otherReview = await host.ReceiveAsync(acquire with { OperationId = Uuid.CreateVersion4() });
        var withoutGrant = await host.ReceiveAsync(complete);
        var notSystem = await host.ReceiveAsync(acquire, ProgramManagementServices.Actor(host.User));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, wrongSource.Error?.Kind);
        Assert.False(wrongSource.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, wrongRevision.Error?.Kind);
        Assert.False(wrongRevision.Error!.IsTransient);
        Assert.False(wrongEvent.Error!.IsTransient);
        Assert.False(otherReview.IsSuccess);
        Assert.False(withoutGrant.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, notSystem.Error?.Kind);
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, report.ReportId)));
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId)));
    }

    [Fact]
    public async Task ShouldFenceLateAcquisitionGivenSettlementBeforeAnyGrant()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        var root = await host.RootAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId), reviewId);
        var intent = ((ProviderReviewRequested)root.Event).Intent;
        var record = await host.PrepareProcessAsync(await host.ReviewKey(reviewId), root, intent);
        var late = await host.CommandAsync(record, nameof(AuthorizeProviderAssuranceReportProviderReviewUse), "acquire");
        var abort = await host.CommandAsync(record with { State = ProcessState.Aborting },
            nameof(AbortProviderReview), "abort");
        Assert.True((await host.ReceiveAsync(abort)).IsSuccess);
        var aborted = await host.ReviewAsync(reviewId);
        var settleSource = NativeFactReference.Of(aborted.Stream, aborted.Canceled!);
        var requirement = (AuthorizeProviderAssuranceReportProviderReviewUse)late;
        var settle = new SettleProviderAssuranceReportProviderReviewUse(Uuid.CreateVersion4(), settleSource,
            requirement.Target, reviewId, requirement.Generation, null, intent, requirement.SourceOwnerId,
            host.ProviderId, reviewId, SourceUseSettlementDisposition.Canceled);
        Assert.True((await host.ReceiveAsync(settle)).IsSuccess);

        // Act
        var delayed = await host.ReceiveAsync(late);

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(delayed.Value);
        var facts = await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, report.ReportId));
        Assert.DoesNotContain(facts, fact => fact is ProviderAssuranceReportProviderReviewUseAuthorized);
        Assert.False((await host.ReportAsync(report.ReportId)).Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldProjectReviewOnlyOnceRecordedGivenProcessCompletion()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        await host.CatchUpAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAssuranceReader>();

        // Act
        var beforeCompletion = await directory.GetReviewAsync(host.Tenant, reviewId);
        await host.DriveReviewAsync(reviewId);
        await host.CatchUpAsync();
        var afterCompletion = await directory.GetReviewAsync(host.Tenant, reviewId);

        // Assert
        Assert.Null(beforeCompletion);
        Assert.Equal(1, afterCompletion!.AssuranceReportRevision);
        Assert.Equal(report.ReportId, afterCompletion.Content.AssuranceReportId);
    }
}
