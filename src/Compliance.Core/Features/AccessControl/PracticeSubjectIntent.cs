using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One practice subject an acceptance or staff assignment frozen at admission: a staff member or a canonical user
///     that will be reserved temporarily and committed permanently for <paramref name="Practice" />. The stream is the
///     subject's own owner stream, whose key orders the acquisition.
/// </summary>
public sealed record PracticeSubjectIntent(PracticeSubjectKind Kind, Uuid SubjectId, string Practice,
    EventStreamAddress Stream)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "practice-subject-intent.v1";

    /// <summary>The canonical sort key of the subject's stream.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Key => PracticeSubjects.Key(Stream);
}
