using System.Reflection;
using Paddock.Application.Access;
using Paddock.Application.Ai;

namespace Paddock.Tests.Architecture;

public class AiIsolationTests
{
    private static readonly string[] ForbiddenNamespaces = ["Paddock.Domain", "Paddock.Simulation"];

    [Fact]
    public void AiNamespaceDoesNotReferenceTruthTypes()
    {
        var aiTypes = typeof(AiActor).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("Paddock.Application.Ai", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(aiTypes);

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in aiTypes)
        {
            var used = new List<Type>();
            if (type.BaseType is not null)
            {
                used.Add(type.BaseType);
            }

            used.AddRange(type.GetInterfaces());
            used.AddRange(type.GetFields(all).Select(f => f.FieldType));
            used.AddRange(type.GetProperties(all).Select(p => p.PropertyType));
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                used.AddRange(method.GetParameters().Select(p => p.ParameterType));
                if (method is MethodInfo info)
                {
                    used.Add(info.ReturnType);
                }
            }

            foreach (var used1 in used.SelectMany(Flatten))
            {
                Assert.False(IsTruth(used1), $"{type.FullName} references truth type {used1.FullName}.");
            }
        }
    }

    [Fact]
    public void AiSourceFilesDoNotUseDomainOrSimulationNamespaces()
    {
        var directory = Path.Combine(RepoPaths.Root(), "src", "Paddock.Application", "Ai");
        var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var forbidden in ForbiddenNamespaces)
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
            }
        }
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

    private static bool IsTruth(Type type) =>
        type.GetCustomAttribute<SimulationTruthAttribute>() is not null
        || (type.Namespace is { } ns && ForbiddenNamespaces.Any(f => ns.StartsWith(f, StringComparison.Ordinal)));
}
