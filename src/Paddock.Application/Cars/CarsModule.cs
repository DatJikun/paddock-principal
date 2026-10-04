using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Domain.Cars;

namespace Paddock.Application.Cars;

/// <summary>
/// T41 cars in the career loop (#160). It registers the car commands. The world initializer already gives every team its two cars
/// (<see cref="InitialCarFactory"/>); a team that has none (a world built some other way, a team that appears later) has its
/// concept approved by the AI manager the first morning it is seen, as a logged <see cref="ApproveConceptCommand"/> that draws the
/// car's ceiling from the Development stream. The concept is the neutral one: choosing a real one is the AI principals' job (T44),
/// so this is a placeholder. A team a human runs is left to the human.
/// </summary>
public sealed class CarsModule : CareerModule
{
    public const string ModuleName = "cars";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [CarsSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => CarCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = context.Session;
        var control = context.Require<IOrganizationControl>();
        var book = new CarBook(() => session.World, session.StoreWorld, session.Clock.MasterSeed);
        context.AddCommandHandlers(dispatcher => CarCommands.Register(dispatcher, book, control));
        context.AddMorning(queue =>
        {
            var today = session.Date;
            var issued = new DateOnly(today.Year, today.Month, today.Day);
            foreach (var team in CareerTeams.Active(session.World, today))
            {
                if (book.Section.Of(team.Id).Count > 0 || !control.Controls(context.Ai, team.Id))
                {
                    continue;
                }

                queue.Enqueue(new ApproveConceptCommand { ManagerId = context.Ai, IssuedOn = issued, OrganizationId = team.Id.Value });
            }
        });
    }
}
