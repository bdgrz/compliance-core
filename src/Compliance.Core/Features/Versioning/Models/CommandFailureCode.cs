namespace Bdgrz.Compliance.Features.Versioning.Models;

public enum CommandFailureCode
{
    MissingRecord,
    StateConflict,
    InvalidContent,
    ActorProhibited,
    VersionConflict,
}
