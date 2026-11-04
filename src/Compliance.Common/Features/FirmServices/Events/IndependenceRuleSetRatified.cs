using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Events;

/// <summary>A retained independence rule version decision.</summary>
[Discriminator("bdgrz.firm-services.rule-set.ratified", 1)]
public sealed record IndependenceRuleSetRatified(long Version, long Revision, FirmServiceReceipt Receipt) : DomainEvent;
