using Paddock.Application.Access;
using Paddock.Application.Spy;
using Paddock.SimRunner;

namespace Paddock.Tests.Spy;

public class SpyTests
{
    private static readonly ManagerId Toy = new("toy-manager");

    [Fact]
    public void SpyIsPassiveStateHashAndRngMatchAcrossSinks()
    {
        var withNull = ToyDecisionLoop.Run(42, 50, NullSink.Instance);
        var memory = new MemorySink();
        var withMemory = ToyDecisionLoop.Run(42, 50, memory);

        Assert.Equal(withNull, withMemory);
        Assert.NotEmpty(memory.Traces);
    }

    [Fact]
    public void TracingConsumesNoRngBeyondTheDecisionDraws()
    {
        // Three draws per step; a fresh run of 0 steps leaves the stream untouched.
        var untouched = ToyDecisionLoop.Run(7, 0, new MemorySink()).FinalRng;
        var traced = ToyDecisionLoop.Run(7, 10, new MemorySink()).FinalRng;
        var plain = ToyDecisionLoop.Run(7, 10, NullSink.Instance).FinalRng;

        Assert.Equal(plain, traced);
        Assert.NotEqual(untouched, traced);
    }

    [Fact]
    public void ProducingTheTraceDoesNotMutateIt()
    {
        var memory = new MemorySink();
        ToyDecisionLoop.Run(1, 3, memory);
        var first = TraceJson.Serialize(memory.Traces[0]);
        _ = WhyView.For(AccessContext.Developer, memory.Traces[0]);
        _ = WhyView.For(AccessContext.ForManager(Toy), memory.Traces[0]);
        Assert.Equal(first, TraceJson.Serialize(memory.Traces[0]));
    }

    [Fact]
    public void TraceRecordsEveryOptionAndTheChoice()
    {
        var memory = new MemorySink();
        ToyDecisionLoop.Run(5, 1, memory);
        var trace = Assert.Single(memory.Traces);

        Assert.Equal(Toy, trace.Who);
        Assert.Equal(3, trace.Options.Count);
        Assert.All(trace.Options, o => Assert.Equal(2, o.Factors.Count));
        var best = trace.Options.MaxBy(o => o.Utility)!;
        Assert.Equal(best.Id, trace.ChosenOptionId);
        Assert.False(string.IsNullOrEmpty(trace.Reason));
        Assert.False(string.IsNullOrEmpty(trace.Trigger));
    }

    [Fact]
    public void MemorySinkIsARingBuffer()
    {
        var sink = new MemorySink(capacity: 3);
        ToyDecisionLoop.Run(1, 4, sink);

        Assert.Equal(3, sink.Traces.Count);
        Assert.Equal("toy-step-1", sink.Traces[0].Trigger);
    }

    [Fact]
    public void MemorySinkKeepsOneWeekendAndRetainsKeyDecisions()
    {
        var sink = new MemorySink();
        ToyDecisionLoop.Run(1, 7, sink); // steps 0-4 weekend 1, 5-6 weekend 2

        Assert.Equal(new WeekendKey(1950, 2), sink.CurrentWeekend);
        Assert.Equal(["toy-step-5", "toy-step-6"], sink.Traces.Select(t => t.Trigger));
        Assert.Equal(["toy-step-0", "toy-step-5"], sink.KeyDecisions.Select(t => t.Trigger));
    }

    [Fact]
    public void MemorySinkRejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemorySink(0));
    }

    [Fact]
    public void NullSinkIsDisabledAndDiscards()
    {
        Assert.False(NullSink.Instance.IsEnabled);
        var memory = new MemorySink();
        ToyDecisionLoop.Run(1, 1, memory);
        NullSink.Instance.Record(memory.Traces[0]);
    }

    [Fact]
    public void FileSinkWritesOneJsonLinePerTrace()
    {
        var path = Path.Combine(Path.GetTempPath(), "spy-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var result = ToyDecisionLoop.Run(3, 4, new FileSink(path));
            var lines = File.ReadAllLines(path);

            Assert.Equal(4, lines.Length);
            Assert.Contains("truth.best-index", lines[0], StringComparison.Ordinal);
            Assert.Equal(ToyDecisionLoop.Run(3, 4, NullSink.Instance), result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WhyViewShowsDeveloperEverything()
    {
        var trace = FirstTrace();
        var view = WhyView.For(AccessContext.Developer, trace)!;

        Assert.Equal(trace.Reason, view.Reason);
        Assert.Equal(trace.TruthContext, view.TruthContext);
        Assert.All(view.Options, o => Assert.Equal(2, o.Factors.Count));
    }

    [Fact]
    public void WhyViewHidesTruthAndHiddenFactorsFromTheOwningManager()
    {
        var trace = FirstTrace();
        var view = WhyView.For(AccessContext.ForManager(Toy), trace)!;

        Assert.Empty(view.TruthContext);
        Assert.Equal(trace.PlayerReason, view.Reason);
        Assert.NotEqual(trace.Reason, view.Reason);
        Assert.All(view.Options, o => Assert.Equal(["raw-draw"], o.Factors.Select(f => f.Name)));
    }

    [Fact]
    public void WhyViewUtilityDoesNotLeakHiddenFactors()
    {
        var baseTrace = FirstTrace();
        var trace = baseTrace with
        {
            Options =
            [
                .. baseTrace.Options.Select((o, i) => o with
                {
                    Utility = 0.6 + i,
                    Factors =
                    [
                        new TraceFactor("raw-draw", 0.5, PlayerVisible: true),
                        new TraceFactor("hidden-bias", 0.1 + i, PlayerVisible: false),
                    ],
                }),
            ],
        };

        foreach (var context in new[] { AccessContext.ForManager(Toy), AccessContext.ForAi(Toy) })
        {
            var view = WhyView.For(context, trace)!;
            Assert.All(view.Options, o =>
            {
                // Utility - visible factors must reveal nothing about the hidden factor.
                Assert.Equal(o.Factors.Sum(f => f.Contribution), o.Utility, 9);
            });
        }

        var developer = WhyView.For(AccessContext.Developer, trace)!;
        Assert.Equal(trace.Options.Select(o => o.Utility), developer.Options.Select(o => o.Utility));
    }

    [Fact]
    public void WhyViewHidesInvisibleOptionsAndTheChoiceIfItIsHidden()
    {
        var trace = FirstTrace() with
        {
            Options = [.. FirstTrace().Options.Select((o, i) => o with { PlayerVisible = i != 0 })],
            ChosenOptionId = "option-0",
        };
        var view = WhyView.For(AccessContext.ForAi(Toy), trace)!;

        Assert.Equal(2, view.Options.Count);
        Assert.DoesNotContain(view.Options, o => o.Id == "option-0");
        Assert.Null(view.ChosenOptionId);
    }

    [Fact]
    public void WhyViewIsNullForAnotherManagersDecision()
    {
        Assert.Null(WhyView.For(AccessContext.ForManager(new ManagerId("rival")), FirstTrace()));
    }

    [Fact]
    public void WhyViewHasNoPlayerReasonWhenNoneIsGiven()
    {
        var trace = FirstTrace() with { PlayerReason = null };
        Assert.Null(WhyView.For(AccessContext.ForManager(Toy), trace)!.Reason);
    }

    [Fact]
    public void ToyCommandPrintsTheSameHashWithAndWithoutSpy()
    {
        var plain = new StringWriter();
        var spied = new StringWriter();
        var err = new StringWriter();

        Assert.Equal(0, ToyCommand.Execute(["toy", "--seed", "42", "--steps", "5"], plain, err));
        Assert.Equal(0, ToyCommand.Execute(["toy", "--seed", "42", "--steps", "5", "--spy"], spied, err));

        var plainLines = Lines(plain);
        var spiedLines = Lines(spied);
        Assert.Single(plainLines);
        Assert.Equal(6, spiedLines.Length);
        Assert.Equal(plainLines[0], spiedLines[0]);
        Assert.Equal(string.Empty, err.ToString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(12)]
    public void ToyCommandSpyPrintsOneTracePerStepAcrossWeekends(int steps)
    {
        var plain = new StringWriter();
        var spied = new StringWriter();
        var err = new StringWriter();
        var stepsText = steps.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(0, ToyCommand.Execute(["toy", "--seed", "42", "--steps", stepsText], plain, err));
        Assert.Equal(0, ToyCommand.Execute(["toy", "--seed", "42", "--steps", stepsText, "--spy"], spied, err));

        var lines = Lines(spied);
        Assert.Equal(steps + 1, lines.Length);
        Assert.Equal(Lines(plain)[0], lines[0]);
        for (var i = 0; i < steps; i++)
        {
            Assert.Contains("toy-step-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), lines[i + 1], StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("toy")]
    [InlineData("toy --seed 1")]
    [InlineData("toy --seed x --steps 1")]
    [InlineData("toy --seed 1 --steps 1 --bogus")]
    [InlineData("toy --seed 1 --seed 2 --steps 1")]
    public void ToyCommandRejectsBadInvocations(string line)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        Assert.Equal(1, ToyCommand.Execute(line.Split(' '), stdout, stderr));
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.NotEqual(string.Empty, stderr.ToString());
    }

    private static DecisionTrace FirstTrace()
    {
        var sink = new MemorySink();
        ToyDecisionLoop.Run(11, 1, sink);
        return sink.Traces[0];
    }

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}
