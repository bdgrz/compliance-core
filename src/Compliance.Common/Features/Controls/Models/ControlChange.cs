using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlChange(string Field, string ChangeType, Uuid? EntryId,
    string? PreviousValue, string? ProposedValue);
