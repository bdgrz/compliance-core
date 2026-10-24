using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Reads and writes the immutable content referenced by campaign, participant and training requirement facts; the
///     realm is the tenant. Every value is the complete original typed structure, so no list is capped by an event
///     size.
/// </summary>
static class PolicyCampaignContent
{
    /// <summary>Where a caller reads the status of an accepted operation.</summary>
    public const string StatusUrlTemplate = "/api/v1/platform/operations/{operation_id}";

    /// <summary>The episode of every first-owner campaign operation.</summary>
    public const string InitialEpisode = "initial";

    public const string TeamsSchema = "PolicyCampaignLaunched.AudienceTeamsManifest.v1";
    public const string EntriesSchema = "PolicyCampaignAudienceFrozen.EntriesManifest.v1";
    public const string AmendmentsSchema = "PolicyCampaignAudienceAmended.AmendmentsManifest.v1";
    public const string TotalsSchema = "PolicyCampaignClosed.TotalsContent.v1";
    public const string ParticipantProofsSchema = "PolicyCampaignClosed.ParticipantProofs.v1";
    public const string RegistrationSchema = "PolicyCampaignLaunched.Registration.v1";
    public const string ReconciliationSchema = "PolicyCampaignAudienceAmended.Reconciliation.v1";
    public const string AcknowledgementSchema = "PolicyAcknowledgementRecorded.Acknowledgement.v1";
    public const string CompletionSchema = "TrainingCompletionRecorded.Completion.v1";
    public const string WaiverSchema = "CampaignWaiverApproved.Waiver.v1";
    public const string CampaignSchema = "PolicyCampaignClosed.Campaign.v1";
    public const string AcknowledgeCommandSchema = "acknowledge-policy.v1";
    public const string CompletionCommandSchema = "record-training-completion.v1";
    public const string WaiverCommandSchema = "approve-campaign-waiver.v1";
    public const string CloseCommandSchema = "close-campaign.v1";
    public const string AdmissionProofSchema = "policy-participant-admission-proof.v1";
    public const string AdmittedResultSchema = "policy-campaign-admitted-result.v1";
    public const string AdmittedResultsSchema = "policy-participant-admitted-results.v1";
    public const string TrainingContentSchema = "TrainingRequirementDefined.ContentContent.v1";
    public const string TrainingRegistrationSchema = "TrainingRequirementDefined.Registration.v1";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId, string schema,
        T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, value, ct);

    /// <summary>Writes content under an id derived from the operation, so a replayed receiver writes identical content.</summary>
    public static Task<ImmutableContentRef> WriteDerivedAsync<T>(IImmutableContentStore content, Uuid tenantId,
        Uuid seed, string tag, string schema, T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion5(seed, tag), schema, value, ct);

    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        CancellationToken ct) where T : notnull => content.ReadJsonAsync<T>(Realm(tenantId), reference, ct);

    /// <summary>
    ///     Writes the complete item list as immutable content and returns the manifest whose count and digest cover
    ///     every item. The digest is that of the exact stored bytes.
    /// </summary>
    public static async Task<ImmutableSelectionManifest> WriteManifestAsync<T>(IImmutableContentStore content,
        Uuid tenantId, string itemSchema, IReadOnlyList<T> items, SelectionOrder order, CancellationToken ct)
        where T : notnull
    {
        var reference = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), itemSchema, items, ct)
            .ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, itemSchema, items.Count, order, reference.Sha256);
    }

    /// <summary>Reads every item of a manifest and verifies its count and digest.</summary>
    public static async Task<IReadOnlyList<T>> ReadManifestAsync<T>(IImmutableContentStore content, Uuid tenantId,
        ImmutableSelectionManifest manifest, CancellationToken ct) where T : notnull
    {
        var items = await content.ReadJsonAsync<IReadOnlyList<T>>(Realm(tenantId), manifest.Content, ct)
            .ConfigureAwait(false);
        if (items.Count != manifest.Count || manifest.ItemsSha256 != manifest.Content.Sha256)
            throw new ImmutableContentIntegrityException("The manifest count or digest does not cover its items.");
        return items;
    }

    /// <summary>Writes the mutation result that wraps an already written response body.</summary>
    public static Task<ImmutableContentRef> WriteResultAsync(IImmutableContentStore content, Uuid tenantId,
        string schema, ImmutableContentRef body, CancellationToken ct) =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(schema, new StandardResponseBody(body)), ct);

    /// <summary>The mutation result of a receiver, content-addressed by the operation so a replay writes the same bytes.</summary>
    public static Task<ImmutableContentRef> WriteDerivedResultAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid seed, string schema, ImmutableContentRef body, CancellationToken ct) =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion5(seed, "mutation-result:" + schema),
            "mutation-result-content.v1", new MutationResultContent(schema, new StandardResponseBody(body)), ct);

    /// <summary>The frozen 200 first response whose body is the given content.</summary>
    public static FirstResponseDescriptor OkResponse(string schema, ImmutableContentRef body) => new(200,
        ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body));

    /// <summary>The frozen 202 first response of a root admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content,
        Uuid tenantId, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var acknowledgment = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(),
            "operation-accepted-response.v1", new OperationAcceptedResponse(operationId,
                StatusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal),
                acceptedAt), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(acknowledgment));
    }

    /// <summary>The typed value of an accepted or replayed 200 admission, read from its frozen first response.</summary>
    public static async Task<Result<T>> ResponseAsync<T>(IImmutableContentStore content, Uuid tenantId,
        Result<AcceptedAppendResult> outcome, CancellationToken ct) where T : notnull
    {
        if (!outcome.IsSuccess)
            return Result<T>.Failure(outcome.Error!);
        var descriptor = DescriptorOf(outcome.Value!);
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A campaign response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The 202 acknowledgment of an accepted or replayed admission, read from the frozen first response.</summary>
    public static Task<Result<OperationAcceptedResponse>> AcknowledgmentAsync(IImmutableContentStore content,
        Uuid tenantId, Result<AcceptedAppendResult> outcome, CancellationToken ct) =>
        ResponseAsync<OperationAcceptedResponse>(content, tenantId, outcome, ct);

    /// <summary>The provenance of a root campaign operation on its own first owner stream.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        EventStreamAddress affectedOwner, ActorReference actor, long? expectedRevision, string intentDigest,
        DateTimeOffset acceptedAt) => new(logicalId, logicalId, null, commandKind, actor, owner, InitialEpisode,
        expectedRevision is { } revision ? (ulong)revision : null, intentDigest, acceptedAt, affectedOwner, owner);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUse.SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, header, owner, acceptedAt);

    /// <summary>The header every campaign effect copies.</summary>
    public static DurableIntentHeader HeaderOf(OperationProvenance provenance, EventStreamAddress firstOwner,
        EventStreamAddress affectedOwner, DateTimeOffset acceptedAt) => new(provenance.RootOperationId, firstOwner,
        affectedOwner, firstOwner, provenance.Actor, provenance.Episode, provenance.OriginalExpectedRevision,
        provenance.AuthoredIntentSha256, acceptedAt);

    public static Uuid ParseTenant(string realm) => Uuid.Parse(realm, CultureInfo.InvariantCulture);

    static FirstResponseDescriptor? DescriptorOf(AcceptedAppendResult result) => result switch
    {
        AcceptedAppendResult.Committed committed => committed.Response,
        AcceptedAppendResult.Replayed replayed => replayed.Response,
        _ => null,
    };
}
