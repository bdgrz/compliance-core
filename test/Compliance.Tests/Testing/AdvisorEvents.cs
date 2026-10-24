using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Builds advisor handoff and feedback facts with their native provenance for projection tests.</summary>
static class AdvisorEvents
{
    public static EventStreamAddress HandoffStream(Uuid tenantId, Uuid handoffId) =>
        new(tenantId.ToString(), AdvisorHandoff.Area, handoffId.ToString());

    public static EventStreamAddress ThreadStream(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId).Stream;

    public static OperationProvenance Provenance(Uuid logicalId, EventStreamAddress owner, ActorReference actor,
        DateTimeOffset at, ulong? expectedRevision = null) => new(logicalId, logicalId, null, "Test", actor, owner,
        "initial", expectedRevision, new string('a', 64), at, owner, owner);

    public static ImmutableContentRef Ref(string schema = "test.v1") =>
        new(Uuid.CreateVersion4(), new string('b', 64), 10, "application/json", schema);

    public static AdvisorHandoffCreated Created(Uuid tenantId, Uuid programId, Uuid handoffId, string purpose,
        DateOnly expiresOn, ActorReference actor, DateTimeOffset at, Uuid? comparedTo = null) => new(tenantId,
        programId, handoffId, Ref(CreatedSchema), purpose, expiresOn, actor, at, comparedTo,
        Provenance(handoffId, HandoffStream(tenantId, handoffId), actor, at), Ref(), Ref());

    public static AdvisorHandoffSelectionRevised Revised(Uuid tenantId, Uuid programId, Uuid handoffId, long revision,
        string reason, ActorReference actor, DateTimeOffset at) => new(tenantId, programId, handoffId, revision,
        Ref(RevisedSchema), reason, actor, at,
        Provenance(Uuid.CreateVersion4(), HandoffStream(tenantId, handoffId), actor, at), Ref(), Ref());

    public static AdvisorHandoffReadiedForReview Readied(Uuid tenantId, Uuid programId, Uuid handoffId, long revision,
        ActorReference actor, DateTimeOffset at) => new(tenantId, programId, handoffId, revision, actor, at,
        Provenance(Uuid.CreateVersion4(), HandoffStream(tenantId, handoffId), actor, at), Ref(), Ref());

    public static AdvisorFeedbackAdmitted Admitted(Uuid tenantId, Uuid handoffId, Uuid feedbackId, long revision,
        long generation, ActorReference actor, DateTimeOffset at) => new(HandoffStream(tenantId, handoffId), revision,
        generation, Ref(ChildAdmissionIntent.Schema), null,
        Provenance(feedbackId, HandoffStream(tenantId, handoffId), actor, at), Ref(), null);

    public static AdvisorFeedbackRecorded Recorded(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        AdvisorRecordVersion source, string kind, string body, ActorReference author, DateTimeOffset at) => new(
        tenantId, programId, handoffId, feedbackId, source.RecordId, source.VersionId, kind, body, author, at,
        Provenance(Uuid.CreateVersion4(), ThreadStream(tenantId, programId, handoffId, feedbackId), author, at),
        Ref(ChildAdmissionIntent.Schema), null, null);

    public static AdvisorFeedbackCorrected Corrected(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        string body, string reason, ActorReference actor, DateTimeOffset at) => new(tenantId, programId, handoffId,
        feedbackId, Uuid.CreateVersion4(), body, reason, actor, at,
        Provenance(Uuid.CreateVersion4(), ThreadStream(tenantId, programId, handoffId, feedbackId), actor, at),
        Ref(), Ref());

    public static AdvisorFeedbackResponded Responded(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        string response, ActorReference actor, DateTimeOffset at) => new(tenantId, programId, handoffId, feedbackId,
        response, actor, at,
        Provenance(Uuid.CreateVersion4(), ThreadStream(tenantId, programId, handoffId, feedbackId), actor, at),
        Ref(), Ref());

    public static AdvisorFeedbackResolved Resolved(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        ActorReference actor, DateTimeOffset at) => new(tenantId, programId, handoffId, feedbackId, actor, at,
        Provenance(Uuid.CreateVersion4(), ThreadStream(tenantId, programId, handoffId, feedbackId), actor, at),
        Ref(), Ref());

    const string CreatedSchema = "AdvisorHandoffCreated.RecordVersionsManifest.v1";
    const string RevisedSchema = "AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1";
}
