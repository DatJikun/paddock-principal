using Paddock.Application.Career;
using Paddock.Domain.Objectives;
using Paddock.Simulation.Objectives;

namespace Paddock.Application.Objectives;

/// <summary>
/// T36 objectives in the career loop. It owns the registry of facts an objective reads (one registration per key; finance adds
/// <c>finance.cash</c>), the objective day handler (order 900), and the settling of whatever outcome no other system applied.
/// It is listed first, because the other modules read its registry while they attach. The championship facts have no supplier yet:
/// the run has no race calendar, so a position objective cannot be shown to be met and counts as not met, as the objective rules say.
/// </summary>
public sealed class ObjectivesModule : CareerModule
{
    public const string ModuleName = "objectives";

    /// <summary>
    /// After the sponsors (750) and the board (910) have applied their effect of an outcome. The board settles the objectives it
    /// applies and needs them still open when it does, so the plain settling runs last and only finds what nobody owned.
    /// </summary>
    public const int SettleOrder = 990;

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [ObjectivesSection.SectionName];

    public override void Configure(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var facts = new ObjectiveFactRegistry();
        context.Provide(facts);
        context.Provide<IObjectiveFacts>(facts);
    }

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = context.Session;
        context.AddDayHandler(new ObjectiveDayHandler(
            () => session.World.Section<ObjectivesSection>(ObjectivesSection.SectionName) ?? ObjectivesSection.Empty,
            context.Require<IObjectiveFacts>()));
        context.AddAfterDay(SettleOrder, events =>
        {
            var section = session.World.Section<ObjectivesSection>(ObjectivesSection.SectionName);
            if (section is null)
            {
                return;
            }

            var settled = ObjectiveOutcomes.Apply(section, events);
            if (!ReferenceEquals(settled, section))
            {
                session.StoreWorld(session.World.WithSection(settled));
            }
        });
    }
}
