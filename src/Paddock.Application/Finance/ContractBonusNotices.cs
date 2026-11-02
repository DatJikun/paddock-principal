using System.Globalization;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Finance;

/// <summary>
/// Tells the human managers of a team that it paid a contract bonus (#355): who it went to, how much and for what. An AI manager
/// gets nothing, as with sponsor notices (#254): it pays the same bonus but has nobody to read an inbox. Not a decision.
/// </summary>
public static class ContractBonusNotices
{
    public static void Post(
        IReadOnlyList<ContractBonusPaid> paid,
        WorldState world,
        GameDate on,
        InboxBook? inbox,
        ManagerRegistry? managers,
        IOrganizationControl? control,
        params (string Name, string Value)[] reason)
    {
        ArgumentNullException.ThrowIfNull(paid);
        ArgumentNullException.ThrowIfNull(world);
        if (paid.Count == 0 || inbox is null || managers is null || control is null)
        {
            return;
        }

        foreach (var bonus in paid)
        {
            var arguments = new List<KeyValuePair<string, string>>
            {
                new("driver", world.GetPerson(bonus.Driver).Name),
                new("driverId", bonus.Driver.Value),
                new("amount", Dollars(bonus.AmountCents)),
                new("organization", bonus.Organization.Value),
            };
            foreach (var (name, value) in reason)
            {
                arguments.Add(new KeyValuePair<string, string>(name, value));
            }

            var subject = bonus.ReasonKey == FinanceReason.TitleBonus ? FinanceKeys.TitleBonusNotice : FinanceKeys.WinBonusNotice;
            foreach (var manager in control.ManagersOf(bonus.Organization))
            {
                if (!managers.Contains(manager) || managers.KindOf(manager) != ManagerKind.Human)
                {
                    continue;
                }

                inbox.Post(managers, manager, new InboxItemDraft(FinanceKeys.BonusNoticeKind, subject, arguments, null, null, null), on);
            }
        }
    }

    public static string Dollars(long cents) =>
        "$" + new Money(cents).WholeDollars.ToString("N0", CultureInfo.InvariantCulture);
}
