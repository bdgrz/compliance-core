using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Events;

/// <summary>A retained independence rule version decision.</summary>
[Discriminator("bdgrz.firm-services.rule-set.draft-revised", 1)]
public sealed record IndependenceRuleSetDraftRevised(long Version, long Revision, IndependenceRuleSetTerms Terms, FirmServiceReceipt Receipt) : DomainEvent;
