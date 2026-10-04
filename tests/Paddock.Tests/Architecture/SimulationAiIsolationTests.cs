using System.Reflection;
using System.Text.RegularExpressions;
using Paddock.Simulation.Ai;

namespace Paddock.Tests.Architecture;

/// <summary>
/// The decision logic of the AI principals lives in <c>Paddock.Simulation.Ai</c> and may only see knowledge (INV-003). The existing
/// <c>AiIsolationTests</c> guard the application-side AI namespace and are untouched; these guard the new one, structurally: no type
/// of the namespace can mention a world, a person, a contract or any truth type, because its inputs are plain numbers and text.
/// </summary>
public class SimulationAiIsolationTests
{
    private static readonly string[] AllowedNamespaces =
    [
        "System",
        "Paddock.Simulation.Ai",
        "Paddock.Domain.Random",
        "Paddock.Domain.Spy",
    ];

    private static IEnumerable<Type> AiTypes() =>
        typeof(UtilityChooser).Assembly.GetTypes()
            .Where(type => type.Namespace == "Paddock.Simulation.Ai" && !type.Name.StartsWith('<'));

    private static bool Allowed(Type type)
    {
        if (type.Namespace is null)
        {
            return true;
        }

        return AllowedNamespaces.Any(allowed => type.Namespace == allowed || type.Namespace.StartsWith(allowed + ".", StringComparison.Ordinal));
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var inner in Flatten(element))
            {
                yield return inner;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var inner in Flatten(argument))
                {
                    yield return inner;
                }
            }
        }
    }

    [Fact]
    public void TheAiNamespaceReferencesOnlyPlainTypesTheRandomStreamAndTheTraceTypes()
    {
        var types = AiTypes().ToList();
        Assert.NotEmpty(types);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in types)
        {
            var used = new List<Type>();
            if (type.BaseType is not null)
            {
                used.Add(type.BaseType);
            }

            used.AddRange(type.GetInterfaces());
            used.AddRange(type.GetFields(all).Select(field => field.FieldType));
            used.AddRange(type.GetProperties(all).Select(property => property.PropertyType));
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                used.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
                if (method is MethodInfo info)
                {
                    used.Add(info.ReturnType);
                }

                var body = method.GetMethodBody();
                if (body is not null)
                {
                    used.AddRange(body.LocalVariables.Select(local => local.LocalType));
                }
            }

            foreach (var mentioned in used.SelectMany(Flatten))
            {
                Assert.True(Allowed(mentioned), $"{type.FullName} mentions {mentioned.FullName}, which is not a knowledge input.");
            }
        }
    }

    [Fact]
    public void TheAiSourceNamesNoTruthQueryNoSystemRandomAndNoClock()
    {
        var directory = Path.Combine(RepoPaths.Root(), "src", "Paddock.Simulation", "Ai");
        var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        string[] forbidden =
        [
            "Paddock.Domain.World", "Paddock.Domain.Contracts", "Paddock.Domain.People", "Paddock.Application", "WorldState", "TruthOf", "PersonTruth",
            "new Random", "System.Random", "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset", "Guid.NewGuid", "Stopwatch",
        ];
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var word in forbidden)
            {
                Assert.DoesNotContain(word, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EveryAiTextKeyIsInBothLanguageFiles()
    {
        var catalog = Paddock.Application.Localization.TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));
        var keys = typeof(AiTextKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
        Assert.NotEmpty(keys);
        foreach (var value in keys)
        {
            // A constant is a translation key, or an option id whose text is its ai.option.* key.
            var key = value.StartsWith("ai.", StringComparison.Ordinal) ? value : AiTextKeys.OptionKey(value);
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.En, key, out _), "en: " + key);
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.Pl, key, out _), "pl: " + key);
        }

        foreach (var split in DevelopmentDecider.Menu)
        {
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.En, AiTextKeys.OptionKey("split/" + split.Name), out _));
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.Pl, AiTextKeys.OptionKey("split/" + split.Name), out _));
        }

        foreach (var name in new[] { "when_ready", "commit_now", "after_races", "next_season" })
        {
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.Pl, AiTextKeys.OptionKey("timing/" + name), out _));
        }
    }

    [Fact]
    public void NoAiPrefixedKeyIsMissingItsPolishTextOrItsEnglishTwin()
    {
        var catalog = Paddock.Application.Localization.TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));
        var pl = catalog.Entries(Paddock.Application.Localization.Language.Pl).Keys.Where(key => key.StartsWith("ai.", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var en = catalog.Entries(Paddock.Application.Localization.Language.En).Keys.Where(key => key.StartsWith("ai.", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(pl);
        Assert.True(pl.SetEquals(en));
        Assert.Matches(new Regex("^ai\\.[A-Za-z_.]+$"), pl.First());
    }
}
