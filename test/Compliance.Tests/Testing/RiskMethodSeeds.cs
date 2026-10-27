using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

static class RiskMethodSeeds
{
    public static async Task<RiskMethod> OwnerAsync(IImmutableContentStore content, RiskMethodRules rules)
    {
        var owner = new RiskMethod(rules.TenantId, rules.ProgramId);
        foreach (var version in rules.Versions)
            await ApplyAsync(content, owner, version);
        return owner;
    }

    public static async Task AppendAsync(IAggregateReader reader, IAggregateWriter writer,
        IImmutableContentStore content, RiskMethodRules rules)
    {
        var owner = await reader.HydrateAsync(new RiskMethod(rules.TenantId, rules.ProgramId));
        foreach (var version in rules.PendingVersions)
        {
            await ApplyAsync(content, owner, version);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
    }

    static async Task ApplyAsync(IImmutableContentStore content, RiskMethod owner, RiskMethodVersionView version)
    {
        var request = new PublishRiskMethodVersion(version.TenantId, version.ProgramId, version.Version - 1,
            version.LikelihoodScale, version.ImpactScale, version.AppetiteThreshold);
        var operation = RootOperation.Provenance(((IIdentifiedRequest)request).RequestId, nameof(PublishRiskMethodVersion),
            owner.Stream, version.PublishedBy, request.ExpectedVersion, AuthoredIntentDigest.Compute(request), version.PublishedAt)
            with
        { FirstOwner = owner.Stream };
        var body = await NativeContent.WriteAsync(content, version.TenantId, RiskMethodContent.Schema,
            new RiskMethodPublicationContent(request, version));
        var response = await NativeContent.FreezeAsync(content, version.TenantId, RiskMethodContent.ResultSchema, version);
        var first = await FirstResponses.FreezeAsync(content, version.TenantId.ToString(), response.Response);
        var outcome = owner.Append(operation, body, first, response.Result);
        Assert.True(outcome.IsSuccess, outcome.Error?.Message);
    }
}
