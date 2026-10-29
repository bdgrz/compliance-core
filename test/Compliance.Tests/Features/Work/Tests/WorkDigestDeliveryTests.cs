using System.Net.Mail;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
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
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WorkDigestDeliveryTests
{
    [Fact]
    public void ShouldRetryOnlyDefiniteTransientSmtpRejectionGivenTransportException()
    {
        // Arrange
        var transient = new SmtpException(SmtpStatusCode.MailboxBusy);
        var permanent = new SmtpException(SmtpStatusCode.MailboxUnavailable);
        var ambiguous = new SmtpException(SmtpStatusCode.GeneralFailure);

        // Act
        var transientOutcome = SmtpWorkDigestDelivery.Classify(transient);
        var permanentOutcome = SmtpWorkDigestDelivery.Classify(permanent);
        var ambiguousOutcome = SmtpWorkDigestDelivery.Classify(ambiguous);

        // Assert
        Assert.Equal(WorkDigestTransportOutcomeKind.DefiniteTransientRejection,
            transientOutcome.Kind);
        Assert.Equal("smtp_transient_rejection", transientOutcome.FailureCode);
        Assert.Equal(WorkDigestTransportOutcomeKind.DefinitePermanentRejection,
            permanentOutcome.Kind);
        Assert.Equal("smtp_permanent_rejection", permanentOutcome.FailureCode);
        Assert.Equal(WorkDigestTransportOutcomeKind.Unknown, ambiguousOutcome.Kind);
        Assert.Equal("transport_ambiguous", ambiguousOutcome.FailureCode);
    }

    [Fact]
    public void ShouldKeepScopeInCanonicalHttpsClientLinkGivenDigestWorkItem()
    {
        // Arrange
        var programId = Uuid.CreateVersion4();
        var workItemId = Uuid.CreateVersion4();
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}", 3,
            TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));

        // Act
        var tenantId = Uuid.CreateVersion4();
        var link = settings.CreateWorkItemUri(tenantId, "acme", programId, workItemId);
        var insecureSettings = settings with
        {
            ApplicationOrigin = "http://app.example",
        };
        var insecure = insecureSettings
            .CreateWorkItemUri(tenantId, "acme", programId, workItemId);
        var missingScopeSettings = settings with
        {
            ClientWorkItemRouteTemplate = "/work/{work_item_id}",
        };
        var missingScope = missingScopeSettings
            .CreateWorkItemUri(tenantId, "acme", programId, workItemId);
        var dotSegmentSettings = settings with
        {
            ClientWorkItemRouteTemplate = "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}/../../../../../../../admin",
        };
        var dotSegment = dotSegmentSettings
            .CreateWorkItemUri(tenantId, "acme", programId, workItemId);
        var encodedDotSegmentSettings = settings with
        {
            ClientWorkItemRouteTemplate = "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}/%252e%252e/admin",
        };
        var encodedDotSegment = encodedDotSegmentSettings
            .CreateWorkItemUri(tenantId, "acme", programId, workItemId);
        var unsafeSlug = settings.CreateWorkItemUri(tenantId, "..", programId, workItemId);

        // Assert
        Assert.NotNull(link);
        Assert.Equal("https", link!.Scheme);
        Assert.Equal("app.example", link.Host);
        Assert.Contains("/acme/programs/", link.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains(tenantId.ToString(), link.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains(programId.ToString(), link.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains(workItemId.ToString(), link.AbsoluteUri, StringComparison.Ordinal);
        Assert.Null(insecure);
        Assert.Null(missingScope);
        Assert.Null(dotSegment);
        Assert.Null(encodedDotSegment);
        Assert.Null(unsafeSlug);
    }
}
