using System.Globalization;
using Paddock.Application.Access;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Objectives;
using Paddock.Application.Racing;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.SimRunner.Scenario;

/// <summary>Player-visible numbers the owner compares across a counterfactual pair. No history comparison (PP-062).</summary>
public sealed record Phase4Snapshot(
    string WorldHash,
    string Date,
    long? CashCents,
    string? ConstructorPosition,
    string Signings,
    string Objectives,
    string Inbox,
    string Events);

public static class Phase4Observables
{
    public static Phase4Snapshot Capture(CareerShell shell)
    {
        var access = AccessContext.ForManager(new ManagerId(shell.Player.Value));
        var team = shell.TeamOf(shell.Player);
        long? cash = null;
        string? position = null;
        if (team is OrganizationId organization)
        {
            if (FinanceQuery.Read(access, organization, shell.Session.World, shell.Date, shell.Modules.Require<IOrganizationControl>()) is FinanceView.Own own)
            {
                cash = own.CashCents;
            }

            var standings = ChampionshipRead.Standings(shell.Session, shell.Modules.Inputs);
            var row = standings.Constructors.FirstOrDefault(item => item.Id == organization.Value);
            if (row is not null)
            {
                position = row.Position.ToString(CultureInfo.InvariantCulture);
            }
        }

        var signings = shell.Session.World.Contracts
            .Where(contract => team is OrganizationId org && contract.OrganizationId == org && contract.IsActiveOn(shell.Date))
            .Select(contract => contract.PersonId.Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var objectives = shell.Modules.TryGet<BoardBook>()?.Objectives.Objectives
            .Where(objective => team is OrganizationId org && objective.Owner == org)
            .Select(objective => objective.Id + ":" + objective.Status)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray() ?? [];
        var inbox = new InboxQuery(shell.Modules.Require<InboxBook>()).View(access).Items
            .Where(item => item.Status == InboxStatus.Open)
            .Select(item => item.Kind + ":" + item.Subject.Key)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
        var years = shell.Session.Years
            .Select(year => year.Year.ToString(CultureInfo.InvariantCulture) + ":" + year.Signed + "/" + year.Renewed + "/" + year.Expired)
            .ToArray();
        return new Phase4Snapshot(
            shell.WorldHash,
            shell.Date.ToString(),
            cash,
            position,
            string.Join(",", signings),
            string.Join(",", objectives),
            string.Join(",", inbox),
            string.Join(",", years));
    }

    public static IReadOnlyList<string> Diverged(Phase4Snapshot left, Phase4Snapshot right)
    {
        var names = new List<string>();
        void Add(string name, string? a, string? b)
        {
            if (!string.Equals(a, b, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        Add("worldHash", left.WorldHash, right.WorldHash);
        Add("date", left.Date, right.Date);
        Add("cash", left.CashCents?.ToString(CultureInfo.InvariantCulture), right.CashCents?.ToString(CultureInfo.InvariantCulture));
        Add("standings", left.ConstructorPosition, right.ConstructorPosition);
        Add("signings", left.Signings, right.Signings);
        Add("objectives", left.Objectives, right.Objectives);
        Add("inbox", left.Inbox, right.Inbox);
        Add("events", left.Events, right.Events);
        return names;
    }
}
