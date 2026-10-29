using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>Verifies the original immutable method body against its native publication and owner.</summary>
public static class RiskMethodContent
{
    public const string Schema = "risk-method-version.v1";
    public const string ResultSchema = "risk-method-publication-result.v1";

    public static bool ValidNative(RiskMethodVersionPublished fact)
    {
        if (fact.TenantId == Uuid.Empty || fact.ProgramId == Uuid.Empty || fact.PublishedVersion < 1 ||
            fact.Content is not { Schema: Schema, MediaType: ImmutableContentStoreExtensions.JsonMediaType, Bytes: >= 0 } ||
            fact.Content.ContentId == Uuid.Empty || fact.Content.Sha256 is not { Length: 64 } || !fact.Content.Sha256.All(Uri.IsHexDigit) ||
            fact.FirstResponse is not { Schema: FirstResponses.Schema } ||
            fact.OperationResult is not { Schema: "mutation-result-content.v1" } ||
            fact.Operation?.Actor is not { Kind: "member" } actor || !Uuid.TryParse(actor.Id, out var member) || member == Uuid.Empty)
            return false;
        var owner = new RiskMethod(fact.TenantId, fact.ProgramId);
        var identity = (IIdentifiedRequest)new PublishRiskMethodVersion(fact.TenantId, fact.ProgramId,
            fact.PublishedVersion - 1, [], []);
        return fact.Operation.FirstOwner == owner.Stream && fact.Operation.LogicalId == identity.RequestId &&
            NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(PublishRiskMethodVersion), (ulong)(fact.PublishedVersion - 1));
    }

    public static async Task<RiskMethodVersionView> ReadAsync(IImmutableContentStore content,
        RiskMethodVersionPublished fact, CancellationToken ct = default)
    {
        if (!ValidNative(fact))
            throw new ImmutableContentIntegrityException("The method source has another exact publication binding.");
        var publication = await NativeContent.ReadAsync<RiskMethodPublicationContent>(content, fact.TenantId, fact.Content, ct).ConfigureAwait(false);
        if (publication is not { Request: { } request, Version: { } version } ||
            request.TenantId != fact.TenantId || request.ProgramId != fact.ProgramId ||
            request.ExpectedVersion != fact.PublishedVersion - 1 ||
            AuthoredIntentDigest.Compute(request) != fact.Operation.AuthoredIntentSha256)
            throw new ImmutableContentIntegrityException("The publication differs from its exact original authored intent.");
        if (version.TenantId != fact.TenantId || version.ProgramId != fact.ProgramId || version.Version != fact.PublishedVersion ||
            version.MethodVersionId != Uuid.CreateVersion5(RiskMethod.IdFor(fact.TenantId, fact.ProgramId),
                version.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)) ||
            version.PublishedBy != fact.Operation.Actor || version.PublishedAt != fact.Operation.AcceptedAt ||
            version.Kind != RiskMethod.QualitativeFiveByFive || version.ReassessmentInterval != RiskMethod.AnnualReassessment ||
            !ValidScale(version.LikelihoodScale) || !ValidScale(version.ImpactScale) || version.AppetiteThreshold is < 1 or > 25)
            throw new ImmutableContentIntegrityException("The immutable method differs from its native identity, original actor, time or rules.");
        var rules = new RiskMethodRules(fact.TenantId, fact.ProgramId);
        var failure = rules.Publish(fact.ProgramId, 0, request.LikelihoodScale, request.ImpactScale,
            request.AppetiteThreshold, fact.Operation.Actor, fact.Operation.AcceptedAt);
        if (failure is not null || rules.Current is not { } first)
            throw new ImmutableContentIntegrityException("The original method publication violates its business rules.");
        var expected = first with { Version = version.Version, MethodVersionId = version.MethodVersionId };
        if (AuthoredIntentDigest.Compute(expected) != AuthoredIntentDigest.Compute(version))
            throw new ImmutableContentIntegrityException("The published method does not reproduce its exact original business transition.");
        return version;
    }

    static bool ValidScale(IReadOnlyList<string>? values) => values is { Count: 5 } && values.All(value =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= 200);
}
