using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Finance;

/// <summary>
/// Recurring finance for one lived day: monthly salaries from active contracts, drafts from <see cref="ILedgerSource"/>
/// (car build, development, supply — empty until those systems exist), then the insolvency watch.
/// Draws no random stream (INV-004). Posts only through <see cref="FinanceSection.Post"/>.
/// </summary>
public sealed class FinanceDayHandler : IDayHandler
{
    public const int DefaultOrder = 800;

    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;
    private readonly IReadOnlyList<ILedgerSource> _sources;

    public FinanceDayHandler(Func<WorldState> read, Action<WorldState> write, IEnumerable<ILedgerSource>? sources = null, int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
        _sources = sources?.ToArray() ?? [];
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = _read();
        var section = world.Section<FinanceSection>(FinanceSection.SectionName);
        if (section is null)
        {
            return;
        }

        var today = context.Today;
        var next = section.AccrueSalaries(world, today);
        if (_sources.Count > 0)
        {
            var drafts = new List<LedgerDraft>();
            foreach (var source in _sources)
            {
                drafts.AddRange(source.Due(today));
            }

            next = next.PostDrafts(drafts, today);
        }

        var (reviewed, insolvent) = next.ReviewInsolvency(today);
        if (!ReferenceEquals(reviewed, section))
        {
            _write(world.WithSection(reviewed));
        }

        foreach (var organization in insolvent)
        {
            context.Emit(FinanceEventTypes.Insolvent, new MarkerPayload(organization.Value));
        }
    }
}
