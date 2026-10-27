using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Commits the permanent practice wall of one reserved subject. <see cref="Source" /> is the irreversible
///     <see cref="EngagementPracticeCommitmentsAuthorized" /> fact; no commitment is ever made while any subject
///     reservation can still fail. The commitment is permanent: it survives every later outcome.
/// </summary>
[Discriminator("bdgrz.practice-independence-commitment.commit-practice-independence", 1)]
public sealed record CommitPracticeIndependence(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    PracticeSubjectKind SubjectKind, Uuid SubjectId, string Practice)
    : IRequest<ReceiverOutcome>, IEngagementAcceptanceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
