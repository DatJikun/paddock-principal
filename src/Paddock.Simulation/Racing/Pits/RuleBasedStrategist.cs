using System.Collections.Immutable;
using System.Globalization;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>
/// The two calculators the strategist scores plans with. They are plain functions so the strategist does not depend on
/// the tyre and fuel models (T30): an adapter around <c>TyreWear.LapLoss</c> and <c>FuelModel.MassPenaltySeconds</c> fits them.
/// </summary>
/// <param name="TyreLossSeconds">
/// <c>(compoundId, ageLaps, fuelKg) =&gt; seconds</c>: seconds a lap is slower than the reference on a set of the compound that has run
/// <c>ageLaps</c> laps (0 for a fresh set), with <c>fuelKg</c> on board. Include the warm-up of a fresh set and the effect of
/// the car's weight on wear if the model has them. Must be deterministic, pure, and finite for any age of at least 0.
/// For a wet compound it is the loss on a DRY track (the strategist weighs it by the chance of a dry track).
/// </param>
/// <param name="FuelMassSeconds">
/// <c>fuelKg =&gt; seconds</c>: seconds a lap is slower with <c>fuelKg</c> on board, for example <c>FuelModel.MassPenaltySeconds</c>.
/// </param>
public sealed record StrategyCalculators(
    Func<string, int, double, double> TyreLossSeconds,
    Func<double, double> FuelMassSeconds);

/// <summary>Search limits of <see cref="RuleBasedStrategist"/>. ESTIMATE defaults.</summary>
/// <param name="MaxStops">Most stops in a candidate plan.</param>
/// <param name="StopShiftVariants">How many times the stop laps are shifted earlier and later.</param>
public sealed record StrategistOptions(
    int MaxStops = PitConstants.MaxPlannedStops,
    int StopShiftVariants = PitConstants.StopShiftVariants);

/// <summary>
/// The default strategist. Every lap it lists candidate plans for the rest of the race (how many stops, on which laps, which
/// compounds, in which pace mode, with or without a driver swap), scores each by the expected time lost, and picks the
/// best first move. Its <c>skill</c> (0 to 100) sets how much it misjudges: it perceives each plan's time with noise of
/// <c>PlanNoiseSdSecondsAtSkill0 * (1 - skill/100)</c> seconds and believes the tyres wear faster or slower than they do by a
/// log-normal factor of sd <c>WearBeliefSdAtSkill0 * (1 - skill/100)</c>. At skill 100 it is exact against its calculators.
/// </summary>
/// <remarks>
/// Time of a plan = sum over the remaining laps of tyre loss + fuel mass + pace-mode effect + driver fatigue + rain mismatch,
/// plus the expected cost of every stop (<see cref="PitStopModel.ExpectedStopSeconds"/>), a place-loss term for each stop when
/// the car behind is closer than the stop costs, and <see cref="PitConstants.InfeasiblePlanPenaltySeconds"/> if the plan
/// runs out of fuel or breaks the mandatory compound mix. All numbers ESTIMATE.
/// <para>
/// Options, scored as the best plan that starts with them: <c>stay_out/{pace}</c> (the first stop is later than now),
/// <c>pit_now/{compound|keep}/{pace}[/swap]</c>. The utility of an option is minus the perceived time (seconds).
/// </para>
/// <para>
/// Randomness: stateless and replayable. The noise comes from a generator derived from the <c>AiDecisions</c> race stream, the
/// car and the lap (the stream is not advanced), so the same snapshot always gives the same decision and asking has no side effect.
/// Weak strategists therefore change their mind from lap to lap, which is the point: that is what misjudgement looks like.
/// </para>
/// </remarks>
public sealed class RuleBasedStrategist : IRaceStrategist
{
    private const double Epsilon = 1e-9;

    private readonly int _skill;
    private readonly StrategyCalculators _calculators;
    private readonly RngStream _stream;
    private readonly StrategistOptions _options;
    private readonly StrategistTracing? _tracing;

    /// <param name="skill">0 to 100.</param>
    /// <param name="calculators">Tyre loss and fuel mass, see <see cref="StrategyCalculators"/>.</param>
    /// <param name="aiDecisionsRaceStream">The race's <c>AiDecisions</c> stream (<see cref="DeriveRaceStream"/>); it is not advanced.</param>
    /// <param name="options">Search limits; the defaults when null.</param>
    /// <param name="tracing">Where to record a <see cref="DecisionTrace"/> per decision; none when null. Passive: it changes no decision and uses no RNG.</param>
    public RuleBasedStrategist(int skill, StrategyCalculators calculators, RngStream aiDecisionsRaceStream, StrategistOptions? options = null, StrategistTracing? tracing = null)
    {
        ArgumentNullException.ThrowIfNull(calculators);
        ArgumentNullException.ThrowIfNull(calculators.TyreLossSeconds);
        ArgumentNullException.ThrowIfNull(calculators.FuelMassSeconds);
        ArgumentNullException.ThrowIfNull(aiDecisionsRaceStream);
        if (skill is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(skill), "Skill must be in [0, 100].");
        }

        _options = options ?? new StrategistOptions();
        if (_options.MaxStops < 0 || _options.StopShiftVariants < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Search limits cannot be negative.");
        }

        _skill = skill;
        _calculators = calculators;
        _stream = aiDecisionsRaceStream;
        _tracing = tracing;
    }

    /// <summary>The strategist's skill, 0 to 100.</summary>
    public int Skill => _skill;

    /// <summary>The race-level <c>AiDecisions</c> stream: derived from the master seed, the season and the round.</summary>
    public static RngStream DeriveRaceStream(ulong masterSeed, int season, int round) =>
        RngStream.Derive(masterSeed, RngStreamName.AiDecisions, season, round);

    public StrategyDecision Decide(KnowledgeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Check(snapshot);
        var planner = new Planner(snapshot, _calculators, _options);

        var carId = snapshot.Car.CarId;
        var rng = _stream.DeriveChild(string.Create(CultureInfo.InvariantCulture, $"strategist:{carId}:lap:{snapshot.Lap}"));
        var unskilled = 1 - (_skill / 100.0);
        var noiseSd = PitConstants.PlanNoiseSdSecondsAtSkill0 * unskilled;
        var belief = Math.Exp(PitConstants.WearBeliefSdAtSkill0 * unskilled * Gaussian.FromUniforms(rng.NextDouble(), rng.NextDouble()));

        var options = new List<(string Id, double Utility, IReadOnlyList<TraceFactor> Factors, Candidate Best)>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in planner.Candidates())
        {
            var cost = planner.Evaluate(candidate, belief);
            var noise = noiseSd * Gaussian.FromUniforms(rng.NextDouble(), rng.NextDouble());
            var utility = -(cost.Total + noise);
            if (index.TryGetValue(candidate.OptionId, out var at))
            {
                if (utility <= options[at].Utility)
                {
                    continue;
                }

                options[at] = (candidate.OptionId, utility, Factors(cost, noise), candidate with { FirstStopRefuelKg = cost.FirstStopRefuelKg });
            }
            else
            {
                index[candidate.OptionId] = options.Count;
                options.Add((candidate.OptionId, utility, Factors(cost, noise), candidate with { FirstStopRefuelKg = cost.FirstStopRefuelKg }));
            }
        }

        var chosenAt = 0;
        for (var i = 1; i < options.Count; i++)
        {
            if (options[i].Utility > options[chosenAt].Utility)
            {
                chosenAt = i;
            }
        }

        var chosen = options[chosenAt];
        var runnerUp = options.Where((_, i) => i != chosenAt).OrderByDescending(o => o.Utility).FirstOrDefault();
        var reason = runnerUp.Id is null
            ? string.Create(CultureInfo.InvariantCulture, $"only option {chosen.Id} ({chosen.Utility:F2} s)")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"best {chosen.Id} ({chosen.Utility:F2} s), runner-up {runnerUp.Id} ({runnerUp.Utility:F2} s), skill {_skill}");
        if (_tracing is { Sink.IsEnabled: true } tracing)
        {
            // Built only from values already computed above; no RNG, no state (INV-006).
            tracing.Sink.Record(new DecisionTrace(
                tracing.Weekend,
                tracing.DeciderId,
                _skill,
                string.Create(CultureInfo.InvariantCulture, $"lap_check:{carId}:lap:{snapshot.Lap}"),
                [.. options.Select(o => new TraceOption(o.Id, o.Utility, o.Factors, PlayerVisible: true))],
                chosen.Id,
                reason,
                PlayerReason: null,
                IsKeyDecision: false,
                TruthContext: new Dictionary<string, string>()));
        }

        var best = chosen.Best;
        if (best.PitNow)
        {
            return new StrategyDecision(StrategyAction.PitNow, snapshot.Rules.TyreChangeAllowed ? best.FirstCompound : null, best.FirstStopRefuelKg, best.SwapFirst, best.Pace);
        }

        return best.Pace == snapshot.Car.PaceMode
            ? new StrategyDecision(StrategyAction.StayOut, null, 0, false, snapshot.Car.PaceMode)
            : new StrategyDecision(StrategyAction.ChangePace, null, 0, false, best.Pace);
    }

    private static IReadOnlyList<TraceFactor> Factors(PlanCost cost, double noise)
    {
        // The strategist's own misjudgement is not something the player's pit wall can see.
        return
        [
            new TraceFactor("tyre_loss", -cost.Tyre, PlayerVisible: true),
            new TraceFactor("fuel_mass", -cost.Mass, PlayerVisible: true),
            new TraceFactor("pace_mode", -cost.Pace, PlayerVisible: true),
            new TraceFactor("stops", -cost.Stops, PlayerVisible: true),
            new TraceFactor("position_risk", -cost.Position, PlayerVisible: true),
            new TraceFactor("driver_fatigue", -cost.Fatigue, PlayerVisible: true),
            new TraceFactor("rain_mismatch", -cost.Rain, PlayerVisible: true),
            new TraceFactor("infeasible", -cost.Infeasible, PlayerVisible: true),
            new TraceFactor("perception_noise", -noise, PlayerVisible: false),
        ];
    }

    private static void Check(KnowledgeSnapshot s)
    {
        if (s.TotalLaps < 1 || s.Lap < 1 || s.Lap > s.TotalLaps)
        {
            throw new ArgumentException("The lap must be within the race.", nameof(s));
        }

        if (s.Compounds.IsDefaultOrEmpty)
        {
            throw new ArgumentException("The snapshot has no compounds.", nameof(s));
        }

        if (!(double.IsFinite(s.ReferenceLapSeconds) && s.ReferenceLapSeconds > 0))
        {
            throw new ArgumentException("The reference lap time must be positive.", nameof(s));
        }

        if (!(double.IsFinite(s.Car.FuelKg) && s.Car.FuelKg >= 0 && double.IsFinite(s.Car.FuelBurnPerLapKg) && s.Car.FuelBurnPerLapKg >= 0))
        {
            throw new ArgumentException("Fuel and burn cannot be negative.", nameof(s));
        }

        if (s.Car.TyreAgeLaps < 0 || s.Car.DriverStintLaps < 0)
        {
            throw new ArgumentException("Ages cannot be negative.", nameof(s));
        }
    }

    /// <summary>A candidate plan: the stops (the lap each is made at the end of, the compound fitted) and the pace mode.</summary>
    private sealed record Candidate(
        string OptionId,
        bool PitNow,
        int[] StopLaps,
        string[] StopCompounds,
        bool SwapFirst,
        PaceMode Pace)
    {
        public double FirstStopRefuelKg { get; init; }

        public string? FirstCompound => StopCompounds.Length > 0 ? StopCompounds[0] : null;
    }

    private sealed class PlanCost
    {
        public double Tyre;
        public double Mass;
        public double Pace;
        public double Stops;
        public double Position;
        public double Fatigue;
        public double Rain;
        public double Infeasible;
        public double FirstStopRefuelKg;

        public double Total => Tyre + Mass + Pace + Stops + Position + Fatigue + Rain + Infeasible;
    }

    private sealed class Planner
    {
        private readonly KnowledgeSnapshot _s;
        private readonly StrategyCalculators _calc;
        private readonly StrategistOptions _options;
        private readonly OwnCarKnowledge _car;
        private readonly PitRules _rules;
        private readonly int _lap;
        private readonly int _total;
        private readonly double[] _rain;
        private readonly bool _wetRace;
        private readonly Dictionary<string, bool> _wetById;
        private readonly string[] _candidateCompounds;
        private readonly int _maxStops;

        public Planner(KnowledgeSnapshot s, StrategyCalculators calc, StrategistOptions options)
        {
            _s = s;
            _calc = calc;
            _options = options;
            _car = s.Car;
            _rules = s.Rules;
            _lap = s.Lap;
            _total = s.TotalLaps;
            _wetById = s.Compounds.ToDictionary(c => c.Id, c => c.IsWet, StringComparer.Ordinal);

            var remaining = _total - _lap + 1;
            _rain = new double[remaining];
            for (var k = 0; k < remaining; k++)
            {
                _rain[k] = RainProbability(k);
            }

            var lookahead = Math.Min(remaining, PitConstants.ForecastLookaheadLaps + 1);
            var maxRain = 0.0;
            for (var k = 0; k < lookahead; k++)
            {
                maxRain = Math.Max(maxRain, _rain[k]);
            }

            _wetRace = _rain[0] >= PitConstants.WetRaceProbability || _car.CompoundsUsed.Any(IsWet);
            var wetCandidates = IsWet(_car.CompoundId) || maxRain >= PitConstants.WetCandidateProbability;
            _candidateCompounds = _rules.TyreChangeAllowed
                ? [.. s.Compounds.Where(c => wetCandidates || !c.IsWet).Select(c => c.Id)]
                : [_car.CompoundId];

            var useful = _rules.TyreChangeAllowed || _rules.RefuellingAllowed;
            var swap = CanSwap;
            _maxStops = remaining < 2 || !(useful || swap)
                ? 0
                : Math.Min(options.MaxStops, useful ? remaining - 1 : 1);
        }

        private bool CanSwap => _rules.DriverChangeAllowed && _car.CanSwapDriver;

        private bool IsWet(string compoundId) => _wetById.TryGetValue(compoundId, out var wet) && wet;

        private double RainProbability(int lapsAhead)
        {
            var forecast = _s.Forecast;
            if (forecast is null || forecast.Steps.IsDefaultOrEmpty)
            {
                return 0;
            }

            var minute = _s.RaceMinute + (lapsAhead * _s.ReferenceLapSeconds / 60.0);
            var at = (int)Math.Round(minute - forecast.FromMinute) - 1;
            return forecast.Steps[Math.Clamp(at, 0, forecast.Steps.Length - 1)].RainProbability;
        }

        public IEnumerable<Candidate> Candidates()
        {
            var paces = new[] { PaceMode.Standard, PaceMode.Push, PaceMode.Save };
            foreach (var pace in paces)
            {
                foreach (var candidate in ForPace(pace))
                {
                    yield return candidate;
                }
            }
        }

        private IEnumerable<Candidate> ForPace(PaceMode pace)
        {
            var paceName = pace.ToString().ToLowerInvariant();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var n = 0; n <= _maxStops; n++)
            {
                foreach (var (stops, now) in Schedules(n))
                {
                    if (!seen.Add((now ? "n" : "s") + string.Join(',', stops)))
                    {
                        continue;
                    }

                    foreach (var compounds in CompoundAssignments(n))
                    {
                        var swaps = n > 0 && CanSwap ? new[] { false, true } : new[] { false };
                        foreach (var swap in swaps)
                        {
                            var id = now
                                ? string.Create(CultureInfo.InvariantCulture, $"pit_now/{(_rules.TyreChangeAllowed ? compounds[0] : "keep")}/{paceName}{(swap ? "/swap" : string.Empty)}")
                                : "stay_out/" + paceName;
                            yield return new Candidate(id, now, stops, compounds, swap, pace);
                        }
                    }
                }
            }
        }

        // Stop schedules for n stops: one that stops now (the first stop is this lap), and evenly spread ones, shifted earlier and later.
        private IEnumerable<(int[] Stops, bool Now)> Schedules(int n)
        {
            if (n == 0)
            {
                yield return ([], false);
                yield break;
            }

            var remaining = _total - _lap + 1;
            var last = _total - 1;

            // Pit now: the first stop is at the end of this lap, the others spread over what is left.
            var now = new int[n];
            now[0] = _lap;
            var after = _total - _lap;
            for (var k = 1; k < n; k++)
            {
                now[k] = _lap + (int)Math.Round((double)k * after / n);
            }

            if (Valid(now, _lap, last))
            {
                yield return (now, true);
            }

            var step = Math.Max(1, (int)Math.Round(remaining / (double)(n + 1) / 6));
            for (var shift = -_options.StopShiftVariants; shift <= _options.StopShiftVariants; shift++)
            {
                var stops = new int[n];
                for (var k = 1; k <= n; k++)
                {
                    stops[k - 1] = _lap - 1 + (int)Math.Round((double)k * remaining / (n + 1)) + (shift * step);
                }

                if (Valid(stops, _lap + 1, last))
                {
                    yield return (stops, false);
                }
            }
        }

        private static bool Valid(int[] stops, int first, int last)
        {
            if (stops[0] < first || stops[^1] > last)
            {
                return false;
            }

            for (var i = 1; i < stops.Length; i++)
            {
                if (stops[i] <= stops[i - 1])
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<string[]> CompoundAssignments(int n)
        {
            if (n == 0)
            {
                yield return [];
                yield break;
            }

            var counter = new int[n];
            while (true)
            {
                var assignment = new string[n];
                for (var i = 0; i < n; i++)
                {
                    assignment[i] = _candidateCompounds[counter[i]];
                }

                yield return assignment;

                var pos = n - 1;
                while (pos >= 0 && ++counter[pos] == _candidateCompounds.Length)
                {
                    counter[pos] = 0;
                    pos--;
                }

                if (pos < 0)
                {
                    yield break;
                }
            }
        }

        public PlanCost Evaluate(Candidate plan, double belief)
        {
            var cost = new PlanCost();
            var compound = _car.CompoundId;
            var wear = (double)_car.TyreAgeLaps;
            var fuel = _car.FuelKg;
            var driverStint = _car.DriverStintLaps;
            var used = new List<string>(_car.CompoundsUsed);
            if (!used.Contains(compound))
            {
                used.Add(compound);
            }

            var (paceDelta, wearFactor, burnFactor) = plan.Pace switch
            {
                PaceMode.Push => (-PitConstants.PushPaceGainSeconds, PitConstants.PushWearFactor, PitConstants.PushBurnFactor),
                PaceMode.Save => (FuelModel.FuelSavingPaceLossSeconds, PitConstants.SaveWearFactor, 1 - TyreFuelConstants.FuelSavingBurnReduction),
                _ => (0.0, 1.0, 1.0),
            };
            var burn = _car.FuelBurnPerLapKg * burnFactor;
            var fatigue = CanSwap;
            var next = 0;
            var outOfFuel = false;

            for (var lap = _lap; lap <= _total; lap++)
            {
                var p = _rain[lap - _lap];
                var loss = _calc.TyreLossSeconds(compound, (int)Math.Round(wear * belief), fuel);
                if (IsWet(compound))
                {
                    loss *= 1 - p;
                }
                else
                {
                    cost.Rain += p * PitConstants.RainOnDryPenaltySeconds;
                }

                cost.Tyre += loss;
                cost.Mass += _calc.FuelMassSeconds(fuel);
                cost.Pace += paceDelta;
                if (fatigue)
                {
                    cost.Fatigue += PitConstants.DriverFatigueSecondsPerLap * Math.Max(0, driverStint - PitConstants.DriverFatigueOnsetLaps);
                }

                if (fuel + Epsilon < burn)
                {
                    outOfFuel = true;
                    fuel = 0;
                }
                else
                {
                    fuel -= burn;
                }

                wear += wearFactor;
                driverStint++;

                if (next < plan.StopLaps.Length && plan.StopLaps[next] == lap)
                {
                    var changeTyres = _rules.TyreChangeAllowed;
                    var nextEnd = next + 1 < plan.StopLaps.Length ? plan.StopLaps[next + 1] : _total;
                    var add = 0.0;
                    if (_rules.RefuellingAllowed)
                    {
                        var target = burn * (nextEnd - lap) * (1 + PitConstants.RefuelSafetyMargin);
                        if (_s.TankCapacityKg is { } cap)
                        {
                            target = Math.Min(target, cap);
                        }

                        add = Math.Max(0, target - fuel);
                    }

                    var swap = next == 0 && plan.SwapFirst;
                    var request = new PitStopRequest(changeTyres, add, swap);
                    var seconds = PitStopModel.ExpectedStopSeconds(_rules, _s.PitLaneLossSeconds, request, _car.Crew);
                    cost.Stops += seconds;
                    if (_s.Gaps.GapBehindSeconds is { } behind && behind < seconds)
                    {
                        cost.Position += PitConstants.PlaceValueSeconds;
                    }

                    if (next == 0)
                    {
                        cost.FirstStopRefuelKg = add;
                    }

                    fuel += add;
                    if (changeTyres)
                    {
                        compound = plan.StopCompounds[next];
                        used.Add(compound);
                        wear = 0;
                    }

                    if (swap)
                    {
                        driverStint = 0;
                    }

                    next++;
                }
            }

            if (outOfFuel || !_rules.MixSatisfied(used, IsWet, _wetRace))
            {
                cost.Infeasible = PitConstants.InfeasiblePlanPenaltySeconds;
            }

            return cost;
        }
    }
}
