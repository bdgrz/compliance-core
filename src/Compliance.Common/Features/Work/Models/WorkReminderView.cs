using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>
///     One in-product reminder: due, overdue, or escalated. The identifier is stable for the item,
///     kind, and due date, so repeated reads never duplicate a reminder.
/// </summary>
public sealed record WorkReminderView(Uuid ReminderId, string Kind, Uuid WorkItemId,
    string Summary, DateOnly? DueOn, int DaysOverdue, string ActionPath);
