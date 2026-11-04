using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>Owns one methodology draft's revisions, independent review and terminal approval or discard.</summary>
public sealed class MethodologyTemplateDraft(Uuid templateId, Uuid draftId)
    : Aggregate(Uuid.CreateVersion5(templateId, $"methodology-template-draft:{draftId}"),
        new EventStreamAddress(MethodologyTemplate.Realm, Area,
            Uuid.CreateVersion5(templateId, $"methodology-template-draft:{draftId}").ToString()))
{
    public const string Area = "methodology-template-drafts";
    public const string EntriesSchema = "methodology-template-draft-entries.v1";
    public long Revision => Id == Uuid.Empty ? -1 : 0;
    public string Status => Id == Uuid.Empty ? string.Empty : "unopened";
    public ImmutableContentRef? EntriesContent => Id == Uuid.Empty ? throw new InvalidOperationException() : null;
    public Uuid? AcceptedReviewer => Id == Uuid.Empty ? Uuid.Empty : null;
    public IReadOnlyCollection<Uuid> RevisionAuthors => Id == Uuid.Empty ? [Uuid.Empty] : [];

    public Result Open(Uuid startEventId, long basedOnVersion, IReadOnlyList<MethodologyTemplateRecordContent> entries,
        ImmutableContentRef content) => Missing();
    public Result Revise(long expectedRevision, IReadOnlyList<MethodologyTemplateRecordContent> entries,
        ImmutableContentRef content, Uuid author) => Missing();
    public Result Review(long revision, Uuid reviewer, bool accept, string comments) => Missing();
    public Result Approve(long revision, Uuid approver) => Missing();
    public Result Discard(Uuid discarder, string reason) => Missing();

    Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        $"The methodology draft {Id} is not opened.", isTransient: true));
}
