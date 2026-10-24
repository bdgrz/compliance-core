using System.Text.Json;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Content;

public sealed class SourceUseReceiverContractTests
{
    [Theory]
    [InlineData("access_authorize")]
    [InlineData("access_settle")]
    [InlineData("policy_authorize")]
    [InlineData("policy_settle")]
    [InlineData("provider_authorize")]
    [InlineData("provider_settle")]
    [InlineData("finding_authorize")]
    public void ShouldBindAuthorityFieldsGivenSelectedWireContract(string kind)
    {
        // Arrange
        var command = Uuid.CreateVersion4();
        var root = Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var stream = new EventStreamAddress("tenant", "owner", owner.ToString());
        var source = new NativeFactReference(stream, Uuid.CreateVersion4(), 1);
        var intent = new ImmutableContentRef(Uuid.CreateVersion4(), new string('a', 64), 1,
            "application/json", "intent.v1");
        object request = kind switch
        {
            "access_authorize" => new AuthorizeAccessReviewItemCampaignCompletionUse(command, source, stream,
                root, 1, null, intent, owner, source.EventId, 17, root),
            "access_settle" => new SettleAccessReviewItemCampaignCompletionUse(command, source, stream,
                root, 1, null, intent, owner, root, SourceUseSettlementDisposition.Canceled),
            "policy_authorize" => new AuthorizePolicyCampaignParticipantCampaignClosureUse(command, source, stream,
                root, 1, null, intent, owner, source.EventId, 17, root, owner),
            "policy_settle" => new SettlePolicyCampaignParticipantCampaignClosureUse(command, source, stream,
                root, 1, null, intent, owner, root, owner, SourceUseSettlementDisposition.Canceled),
            "provider_authorize" => new AuthorizeProviderAssuranceReportProviderReviewUse(command, source, stream,
                root, 1, null, intent, owner, source.EventId, 17, owner, root),
            "provider_settle" => new SettleProviderAssuranceReportProviderReviewUse(command, source, stream,
                root, 1, null, intent, owner, owner, root, SourceUseSettlementDisposition.Canceled),
            "finding_authorize" => new AuthorizeCorrectiveActionFindingClosureUse(command, source, stream,
                root, 1, null, intent, owner, source.EventId, 17, root, owner),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var type = ComplianceCoreJsonContext.Default.GetTypeInfo(request.GetType())!;
        var wire = JsonSerializer.SerializeToNode(request, type)!;
        var receiving = Uuid.CreateVersion4();
        wire["receiving_owner_id"] = receiving.ToString();
        wire["source_revision"] = 23;

        // Act
        var decoded = wire.Deserialize(type)!;
        var actualReceiving = decoded switch
        {
            AuthorizeAccessReviewItemCampaignCompletionUse value => value.CampaignId,
            SettleAccessReviewItemCampaignCompletionUse value => value.CampaignId,
            AuthorizePolicyCampaignParticipantCampaignClosureUse value => value.CampaignId,
            SettlePolicyCampaignParticipantCampaignClosureUse value => value.CampaignId,
            AuthorizeProviderAssuranceReportProviderReviewUse value => value.ReviewId,
            SettleProviderAssuranceReportProviderReviewUse value => value.ReviewId,
            AuthorizeCorrectiveActionFindingClosureUse value => value.ReceivingOwnerId,
            _ => throw new InvalidOperationException(),
        };

        // Assert
        Assert.Equal(receiving, actualReceiving);
        if (decoded is ISourceUseAuthorization authorization)
            Assert.Equal(23UL, authorization.ExpectedContentRevision);
    }
}
