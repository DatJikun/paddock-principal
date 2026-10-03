using Paddock.Application.Localization;
using Paddock.SimRunner;

namespace Paddock.Tests.Localization;

public class LocalizationTests
{
    private static string I18nDir() => Path.Combine(RepoPaths.Root(), "data", "i18n");

    private static IReadOnlyDictionary<string, object?> Args(params (string Name, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Name, p => p.Value);

    private static TranslationCatalog Catalog(string pl, string en) =>
        new(TranslationLoader.Parse(pl, "pl"), TranslationLoader.Parse(en, "en"));

    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(2, PluralCategory.Few)]
    [InlineData(4, PluralCategory.Few)]
    [InlineData(5, PluralCategory.Many)]
    [InlineData(12, PluralCategory.Many)]
    [InlineData(14, PluralCategory.Many)]
    [InlineData(21, PluralCategory.Many)]
    [InlineData(22, PluralCategory.Few)]
    [InlineData(25, PluralCategory.Many)]
    [InlineData(112, PluralCategory.Many)]
    [InlineData(0, PluralCategory.Many)]
    [InlineData(1.5, PluralCategory.Other)]
    public void PolishPluralBoundaries(double count, PluralCategory expected) =>
        Assert.Equal(expected, PluralRules.Select(Language.Pl, count));

    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(0, PluralCategory.Other)]
    [InlineData(2, PluralCategory.Other)]
    [InlineData(1.5, PluralCategory.Other)]
    public void EnglishPluralRules(double count, PluralCategory expected) =>
        Assert.Equal(expected, PluralRules.Select(Language.En, count));

    [Fact]
    public void FormatNumberUsesLanguageSeparators()
    {
        Assert.Equal("1 234,5", NumberFormatter.FormatNumber(1234.5, Language.Pl));
        Assert.Equal("1,234.5", NumberFormatter.FormatNumber(1234.5, Language.En));
        Assert.Equal("3", NumberFormatter.FormatNumber(3, Language.Pl));
    }

    private const string PlJson = """
        {
          "hello": "Cześć, {driver}!",
          "only.pl": "Tylko po polsku",
          "drivers": { "one": "{count} kierowca", "few": "{count} kierowców", "many": "{count} kierowców", "other": "{count} kierowcy" },
          "braces": "Klamry {{ok}} i {x}"
        }
        """;

    private const string EnJson = """
        {
          "hello": "Hello, {driver}!",
          "only.en": "English only",
          "drivers": { "one": "{count} driver", "other": "{count} drivers" },
          "braces": "Braces {{ok}} and {x}"
        }
        """;

    [Fact]
    public void GetFormatsNamedArguments()
    {
        var sink = new CollectingMissingKeySink();
        var pl = new Localizer(Catalog(PlJson, EnJson), Language.Pl, sink);
        Assert.Equal("Cześć, Fangio!", pl.Get("hello", Args(("driver", "Fangio"))));
        Assert.Equal("Klamry {ok} i 7", pl.Get("braces", Args(("x", 7))));
        Assert.Empty(sink.Reports);
    }

    [Fact]
    public void GetPluralPicksCategoryAndFormatsCount()
    {
        var sink = new CollectingMissingKeySink();
        var catalog = Catalog(PlJson, EnJson);
        var pl = new Localizer(catalog, Language.Pl, sink);
        var en = new Localizer(catalog, Language.En, sink);
        Assert.Equal("1 kierowca", pl.GetPlural("drivers", 1));
        Assert.Equal("3 kierowców", pl.GetPlural("drivers", 3));
        Assert.Equal("1,5 kierowcy", pl.GetPlural("drivers", 1.5));
        Assert.Equal("1 driver", en.GetPlural("drivers", 1));
        Assert.Equal("1,000 drivers", en.GetPlural("drivers", 1000));
        Assert.Empty(sink.Reports);
    }

    [Fact]
    public void FallsBackToEnglishThenKeyAndReportsBoth()
    {
        var sink = new CollectingMissingKeySink();
        var pl = new Localizer(Catalog(PlJson, EnJson), Language.Pl, sink);

        Assert.Equal("English only", pl.Get("only.en"));
        Assert.Equal("nope.nothing", pl.Get("nope.nothing"));

        Assert.Equal(
            [MissingKeyKind.FallbackToEnglish, MissingKeyKind.MissingEverywhere],
            sink.Reports.Select(r => r.Kind));
        Assert.All(sink.Reports, r => Assert.Equal(Language.Pl, r.Language));
    }

    [Fact]
    public void MissingArgumentIsReportedAndPlaceholderKept()
    {
        var sink = new CollectingMissingKeySink();
        var en = new Localizer(Catalog(PlJson, EnJson), Language.En, sink);
        Assert.Equal("Hello, {driver}!", en.Get("hello"));
        var report = Assert.Single(sink.Reports);
        Assert.Equal(MissingKeyKind.MissingArgument, report.Kind);
        Assert.Equal("driver", report.Detail);
    }

    [Fact]
    public void MissingPluralFormFallsBackToOtherAndIsReported()
    {
        var sink = new CollectingMissingKeySink();
        var catalog = Catalog(
            """{ "k": { "other": "{count} x" } }""",
            """{ "k": { "other": "{count} x" } }""");
        var pl = new Localizer(catalog, Language.Pl, sink);
        Assert.Equal("1 x", pl.GetPlural("k", 1));
        Assert.Equal(MissingKeyKind.MissingPluralForm, Assert.Single(sink.Reports).Kind);
    }

    [Theory]
    [InlineData("""{ "a": 1 }""")]
    [InlineData("""{ "a": { "few": "x" } }""")]
    [InlineData("""{ "a": { "zero": "x", "other": "y" } }""")]
    [InlineData("""{ "a": "x", "a": "y" }""")]
    [InlineData("""[]""")]
    [InlineData("""{ nope""")]
    public void LoaderRejectsMalformedFiles(string json) =>
        Assert.Throws<LocalizationLoadException>(() => TranslationLoader.Parse(json, "test"));

    [Fact]
    public void CheckerFindsKeySetPlaceholderEmptyAndPluralProblems()
    {
        var catalog = Catalog(
            """{ "a": "x {n}", "b": "", "c": { "one": "1", "other": "n" }, "only.pl": "x" }""",
            """{ "a": "x", "b": "ok", "c": { "one": "1", "other": "n" }, "only.en": "x" }""");
        var problems = I18nChecker.CheckCatalog(catalog);

        Assert.Contains(problems, p => p.Contains("'only.pl' is in pl.json but not in en.json"));
        Assert.Contains(problems, p => p.Contains("'only.en' is in en.json but not in pl.json"));
        Assert.Contains(problems, p => p.Contains("placeholder {n} is in pl but not in en"));
        Assert.Contains(problems, p => p.Contains("pl.json: 'b' is empty"));
        Assert.Contains(problems, p => p.Contains("pl.json: 'c' lacks the 'few' plural form"));
        Assert.Contains(problems, p => p.Contains("pl.json: 'c' lacks the 'many' plural form"));
    }

    [Fact]
    public void ScannerFindsAttributedConstsConstructedKeysAndConfigErrors()
    {
        var source = """
            class A
            {
                [TranslationKey]
                public const string One = "a.one";
                [TranslationKey] internal static readonly string NotConst = "ignored";
                var k = new TranslationKey("a.two");
                var e = "config.error.bad-seed";
                const string Plain = "not.a.key";
            }
            """;
        var keys = TranslationKeyScanner.ScanSource(source, "A.cs").Select(k => k.Key).Order().ToList();
        Assert.Equal(["a.one", "a.two", "config.error.bad-seed"], keys);
    }

    [Fact]
    public void ShippedFilesPassAllChecks() =>
        Assert.Empty(I18nChecker.CheckRepository(RepoPaths.Root()));

    [Fact]
    public void EveryKeyReferencedInSourceExistsInBothFiles()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        var catalog = TranslationLoader.LoadDirectory(I18nDir());
        var authored = TranslationKeyScanner.ScanAuthoredErrorCodes(src);
        Assert.True(authored.Count >= 28, "expected the authored validator codes to be found");
        Assert.Empty(I18nChecker.CheckKeysPresent(catalog, TranslationKeyScanner.ScanDirectory(src).Concat(authored)));
    }

    [Fact]
    public void ScanReportsAKeyMissingFromOneFile()
    {
        var catalog = Catalog("""{ "a": "x" }""", """{ "a": "x", "b": "y" }""");
        var problems = I18nChecker.CheckKeysPresent(catalog, [new ScannedKey("b", "X.cs")]);
        Assert.Equal("key 'b' (used in X.cs) is missing in pl.json", Assert.Single(problems));
    }

    [Fact]
    public void I18nCheckCommandPassesOnTheRepository()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = I18nCheckCommand.Execute(["i18n-check", "--root", RepoPaths.Root()], stdout, stderr);
        Assert.Equal(0, code);
        Assert.Equal("", stderr.ToString());
    }

    [Fact]
    public void I18nCheckCommandFailsOnBrokenFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "pp-i18n-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "data", "i18n"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        try
        {
            File.WriteAllText(Path.Combine(root, "data", "i18n", "pl.json"), """{ "a": "x" }""");
            File.WriteAllText(Path.Combine(root, "data", "i18n", "en.json"), """{ "b": "x" }""");
            var stderr = new StringWriter();
            var code = I18nCheckCommand.Execute(["i18n-check", "--root", root], new StringWriter(), stderr);
            Assert.Equal(1, code);
            Assert.Contains("'a' is in pl.json but not in en.json", stderr.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
