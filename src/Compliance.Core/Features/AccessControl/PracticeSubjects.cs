using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Stream names of the permanent practice subjects.</summary>
public static class PracticeSubjects
{
    /// <summary>The wire name of a subject kind.</summary>
    public static string Wire(PracticeSubjectKind kind) => kind switch
    {
        PracticeSubjectKind.Staff => "staff",
        PracticeSubjectKind.CanonicalUser => "canonical_user",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The stream resource of one subject: its kind and identity, so the two kinds never share a key.</summary>
    public static string Resource(PracticeSubjectKind kind, Uuid subjectId) =>
        $"{Wire(kind)}:{subjectId.ToString()}";

    /// <summary>The stream of one subject of one client.</summary>
    public static EventStreamAddress Stream(Uuid tenantId, PracticeSubjectKind kind, Uuid subjectId) =>
        new(tenantId.ToString(), PracticeIndependenceCommitment.Area, Resource(kind, subjectId));

    /// <summary>The canonical sort key of one stream: its realm, area and resource.</summary>
    public static string Key(EventStreamAddress stream) => $"{stream.Realm}/{stream.Area}/{stream.Resource}";

    /// <summary>Parses the kind and identity out of a subject stream resource, or null when it is not one.</summary>
    public static (PracticeSubjectKind Kind, Uuid SubjectId)? Parse(string resource)
    {
        var separator = resource.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0 || !Uuid.TryParse(resource[(separator + 1)..], CultureInfo.InvariantCulture, out var id))
            return null;
        return resource[..separator] switch
        {
            "staff" => (PracticeSubjectKind.Staff, id),
            "canonical_user" => (PracticeSubjectKind.CanonicalUser, id),
            _ => null,
        };
    }
}
