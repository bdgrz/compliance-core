using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Builds projection facts with complete immutable authored bodies and exact original native provenance.</summary>
static class AdvisorEvents
{
    public static EventStreamAddress HandoffStream(Uuid tenantId, Uuid handoffId) =>
        new(tenantId.ToString(), AdvisorHandoff.Area, handoffId.ToString());

    public static EventStreamAddress ThreadStream(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId).Stream;

    public static OperationProvenance Provenance(Uuid logicalId, EventStreamAddress owner, ActorReference actor,
        DateTimeOffset at, ulong? expectedRevision = null) => new(logicalId, logicalId, null, "Test", actor, owner,
        "initial", expectedRevision, new string('a', 64), at, owner, owner, owner);

    // These placeholders are used only by facts whose transport bodies are not decoded by these projection fixtures.
    public static ImmutableContentRef Ref(string schema = "test.v1") =>
        new(Uuid.CreateVersion4(), new string('b', 64), 10, "application/json", schema);

    public static async Task<AdvisorHandoffCreated> Created(IImmutableContentStore content,
        IReadOnlyList<AdvisorRecordVersion> selection, Uuid tenantId, Uuid programId, Uuid handoffId, string purpose,
        DateOnly expiresOn, ActorReference actor, DateTimeOffset at, Uuid? comparedTo = null)
    {
        var request = new CreateAdvisorHandoff(tenantId, programId, handoffId, selection, purpose, expiresOn, comparedTo);
        var authored = await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None);
        var manifest = await AdvisorContent.WriteSelectionAsync(content, tenantId, AdvisorContent.CreatedSelectionSchema,
            selection, CancellationToken.None);
        var frozen = await UnitAsync(content, tenantId);
        return new AdvisorHandoffCreated(tenantId, programId, handoffId, manifest, authored, expiresOn, actor, at,
            comparedTo, Root(handoffId, HandoffStream(tenantId, handoffId), actor, at, null, request), frozen.First,
            frozen.Result);
    }

    public static async Task<AdvisorHandoffSelectionRevised> Revised(IImmutableContentStore content,
        IReadOnlyList<AdvisorRecordVersion> selection, Uuid tenantId, Uuid programId, Uuid handoffId, long revision,
        string reason, ActorReference actor, DateTimeOffset at)
    {
        var command = Uuid.CreateVersion4();
        var request = new ReviseAdvisorHandoffSelection(command, tenantId, programId, handoffId, revision - 1,
            selection, reason);
        var authored = await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None);
        var manifest = await AdvisorContent.WriteSelectionAsync(content, tenantId, AdvisorContent.RevisedSelectionSchema,
            selection, CancellationToken.None);
        var frozen = await UnitAsync(content, tenantId);
        return new AdvisorHandoffSelectionRevised(tenantId, programId, handoffId, revision, manifest, authored, actor, at,
            Root(command, HandoffStream(tenantId, handoffId), actor, at, (ulong)request.ExpectedRevision, request),
            frozen.First, frozen.Result);
    }

    public static AdvisorHandoffReadiedForReview Readied(Uuid tenantId, Uuid programId, Uuid handoffId, long revision,
        ActorReference actor, DateTimeOffset at)
    {
        var command = Uuid.CreateVersion4();
        var request = new MarkAdvisorHandoffReadyForReview(command, tenantId, programId, handoffId, revision - 1);
        return new(tenantId, programId, handoffId, revision, actor, at,
            Root(command, HandoffStream(tenantId, handoffId), actor, at, (ulong)request.ExpectedRevision, request), Ref(), Ref());
    }

    /// <summary>Serialization-only admission shape; these projection paths do not decode this opaque intent.</summary>
    public static AdvisorFeedbackAdmitted Admitted(Uuid tenantId, Uuid handoffId, Uuid feedbackId, long revision,
        long generation, ActorReference actor, DateTimeOffset at) => new(HandoffStream(tenantId, handoffId), revision,
        generation, Ref(ChildAdmissionIntent.Schema), null,
        Provenance(feedbackId, HandoffStream(tenantId, handoffId), actor, at, (ulong)revision - 1) with
        { CommandKind = nameof(RecordAdvisorFeedback) }, Ref(), null);

    public static async Task<AdvisorFeedbackRecorded> Recorded(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid handoffId, Uuid feedbackId, AdvisorRecordVersion source, string kind, string body,
        ActorReference author, DateTimeOffset at, long expectedHandoffRevision = 2, ulong parentGeneration = 1)
    {
        var selected = source.SourceKind is null ? null : source;
        var request = new RecordAdvisorFeedback(tenantId, programId, handoffId, feedbackId, source.RecordId,
            source.VersionId, kind, body, expectedHandoffRevision)
        { SelectedSource = selected };
        var parent = HandoffStream(tenantId, handoffId);
        var root = Root(feedbackId, parent, author, at, (ulong)expectedHandoffRevision, request);
        var header = AdvisorContent.Header(root, at);
        var authored = await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None);
        var proofs = await AdvisorContent.EmptyProofsAsync(content, tenantId, feedbackId, CancellationToken.None);
        var intent = await AdvisorContent.WriteAsync(content, tenantId, ChildAdmissionIntent.Schema,
            new ChildAdmissionIntent(header, handoffId, parentGeneration, AdmittedChildKind.AdvisorFeedback,
                feedbackId, authored, proofs, (ulong)expectedHandoffRevision + 1), CancellationToken.None);
        var target = ThreadStream(tenantId, programId, handoffId, feedbackId);
        var child = new OperationProvenance(Uuid.CreateVersion4(), feedbackId, Uuid.CreateVersion4(),
            nameof(MaterializeAdvisorFeedback), author, target, root.Episode, root.OriginalExpectedRevision,
            root.AuthoredIntentSha256, at.AddTicks(1), target, target, parent);
        return new AdvisorFeedbackRecorded(tenantId, programId, handoffId, feedbackId, source.RecordId,
            source.VersionId, kind, author, at, child, intent, null, null)
        { SelectedSource = selected };
    }

    public static async Task<AdvisorFeedbackCorrected> Corrected(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid handoffId, Uuid feedbackId, string body, string reason, ActorReference actor,
        DateTimeOffset at, long expectedRevision = 1)
    {
        var id = Uuid.CreateVersion4();
        var request = new CorrectAdvisorFeedback(tenantId, programId, handoffId, feedbackId, id, body, reason, expectedRevision);
        var authored = await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None);
        var frozen = await UnitAsync(content, tenantId);
        return new(tenantId, programId, handoffId, feedbackId, id, authored, actor, at,
            Root(id, ThreadStream(tenantId, programId, handoffId, feedbackId), actor, at, (ulong)expectedRevision, request),
            frozen.First, frozen.Result);
    }

    public static async Task<AdvisorFeedbackResponded> Responded(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid handoffId, Uuid feedbackId, string response, ActorReference actor, DateTimeOffset at,
        long expectedRevision = 1)
    {
        var id = Uuid.CreateVersion4();
        var request = new RespondToAdvisorFeedback(id, tenantId, programId, handoffId, feedbackId, response, expectedRevision);
        var authored = await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None);
        var frozen = await UnitAsync(content, tenantId);
        return new(tenantId, programId, handoffId, feedbackId, authored, actor, at,
            Root(id, ThreadStream(tenantId, programId, handoffId, feedbackId), actor, at, (ulong)expectedRevision, request),
            frozen.First, frozen.Result);
    }

    public static AdvisorFeedbackResolved Resolved(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        ActorReference actor, DateTimeOffset at, long expectedRevision = 2)
    {
        var id = Uuid.CreateVersion4();
        var request = new ResolveAdvisorFeedback(id, tenantId, programId, handoffId, feedbackId, expectedRevision);
        return new(tenantId, programId, handoffId, feedbackId, actor, at,
            Root(id, ThreadStream(tenantId, programId, handoffId, feedbackId), actor, at, (ulong)expectedRevision, request), Ref(), Ref());
    }

    static OperationProvenance Root<T>(Uuid logicalId, EventStreamAddress owner, ActorReference actor,
        DateTimeOffset at, ulong? expectedRevision, T request) where T : notnull =>
        Provenance(logicalId, owner, actor, at, expectedRevision) with
        { CommandKind = typeof(T).Name, AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request) };

    static async Task<(ImmutableContentRef First, ImmutableContentRef Result)> UnitAsync(IImmutableContentStore content, Uuid tenant)
    {
        var frozen = await AdvisorContent.FreezeUnitAsync(content, tenant, CancellationToken.None);
        return (await FirstResponses.FreezeAsync(content, tenant.ToString(), frozen.Response), frozen.Result);
    }
}
