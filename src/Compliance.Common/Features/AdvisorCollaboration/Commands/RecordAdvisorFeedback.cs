using Cntryl.Portia;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>
///     Admits attributed consultant feedback on an exact selected version of a ready handoff.
///     <paramref name="FeedbackId" /> is the logical operation identity and the feedback identity;
///     <paramref name="ExpectedHandoffRevision" /> is the revision of the handoff, which owns the admission, and never
///     a revision of the feedback thread. The complete feedback is frozen on the handoff before the 202
///     acknowledgment; the thread is materialized independently.
/// </summary>
[Discriminator("bdgrz.advisor-collaboration.record-advisor-feedback", 2)]
public sealed record RecordAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, Uuid RecordId, Uuid RecordVersionId, string Kind, string Body, long ExpectedHandoffRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    /// <summary>The native selected identity for sources whose version is not a UUID pair.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdvisorRecordVersion? SelectedSource { get; init; }

    Uuid IIdentifiedRequest.RequestId => FeedbackId;
}
