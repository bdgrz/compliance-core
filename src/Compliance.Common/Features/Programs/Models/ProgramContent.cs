using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>The complete authored name and dated plan of one program revision, retained as verified immutable content.</summary>
public sealed record ProgramContent(string Name, ProgramPlan Plan)
{
    /// <summary>Schema of the immutable program revision value.</summary>
    public const string Schema = "program-content.v1";
}
