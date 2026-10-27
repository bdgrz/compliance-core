using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProgramSetupWorkItem(string Code, string Detail,
    string SourceType, Uuid? SourceId);
