using System.Reflection;
using System.Text.RegularExpressions;

namespace OcrAcl.Tests.Architecture;

public static class LayerScanHelper
{
    private static readonly string[] ForbiddenForApplication =
    {
        "OcrAcl.Api",
        "OcrAcl.Infrastructure",
        "Microsoft.AspNetCore"
    };

    private static readonly string[] ForbiddenForInfrastructure =
    {
        "OcrAcl.Api",
        "Microsoft.AspNetCore.Mvc"
    };

    private static readonly string[] ForbiddenForApi =
    {
        "OcrAcl.Infrastructure"
    };

    public static IReadOnlyList<string> FindViolations(string layer, string source)
    {
        var forbidden = layer switch
        {
            "Application" => ForbiddenForApplication,
            "Infrastructure" => ForbiddenForInfrastructure,
            "Api" => ForbiddenForApi,
            _ => Array.Empty<string>()
        };

        var violations = new List<string>();
        var lines = source.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var token in forbidden)
            {
                if (ContainsReference(line, token))
                {
                    violations.Add($"line {i + 1}: {token}");
                }
            }
        }

        return violations;
    }

    private static bool ContainsReference(string line, string token)
    {
        if (Regex.IsMatch(line, $@"\busing\s+{Regex.Escape(token)}\b"))
        {
            return true;
        }

        return Regex.IsMatch(line, $@"\b{Regex.Escape(token)}\.");
    }

    public static string? FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OcrAcl.sln")) ||
                File.Exists(Path.Combine(directory.FullName, "OcrAcl.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

public sealed class LayerRuleTests
{
    private static readonly string[] ForbiddenNamespaces =
    {
        "OcrAcl.Api",
        "OcrAcl.Infrastructure",
        "Microsoft.AspNetCore"
    };

    private static Assembly ApiAssembly => typeof(OcrAcl.Application.Models.OcrDocument).Assembly;

    private static IEnumerable<Type> TypesInNamespace(string ns) =>
        ApiAssembly.GetTypes().Where(t => t.Namespace == ns || (t.Namespace?.StartsWith(ns + ".", StringComparison.Ordinal) ?? false));

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var results = new List<Type>();

        void Add(Type? candidate)
        {
            if (candidate is null)
            {
                return;
            }

            results.Add(candidate);
            if (candidate.IsGenericType)
            {
                foreach (var argument in candidate.GetGenericArguments())
                {
                    results.Add(argument);
                }
            }
        }

        Add(type.BaseType);
        foreach (var iface in type.GetInterfaces())
        {
            Add(iface);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            Add(field.FieldType);
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            Add(property.PropertyType);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Add(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                Add(parameter.ParameterType);
            }
        }

        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                Add(parameter.ParameterType);
            }
        }

        return results;
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-001")]
    [Trait("Category", "ARC")]
    public void ApplicationTypes_DoNotReferenceForbiddenLayers()
    {
        var applicationTypes = TypesInNamespace("OcrAcl.Application").ToArray();
        Assert.NotEmpty(applicationTypes);                                  // T-27 point 4

        var violations = new List<string>();
        foreach (var type in applicationTypes)
        {
            foreach (var referenced in ReferencedTypes(type))
            {
                var ns = referenced.Namespace ?? string.Empty;
                if (ForbiddenNamespaces.Any(forbidden =>
                        ns == forbidden || ns.StartsWith(forbidden + ".", StringComparison.Ordinal)))
                {
                    violations.Add($"{type.FullName} -> {referenced.FullName}");
                }
            }
        }

        Assert.Empty(violations);                                           // T-27 point 1
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-002")]
    [Trait("Category", "ARC")]
    public void LayerSources_DoNotContainForbiddenUsings()
    {
        var root = LayerScanHelper.FindSolutionRoot();
        Assert.NotNull(root);                                               // T-27 point 2 (never skip)
        var sourceRoot = Path.Combine(root!, "src", "OcrAcl.Api");

        var violations = new List<string>();
        foreach (var layer in new[] { "Api", "Application", "Infrastructure" })
        {
            var files = Directory.GetFiles(Path.Combine(sourceRoot, layer), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                            !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToArray();

            Assert.NotEmpty(files);                                         // T-27 point 4

            foreach (var file in files)
            {
                if (layer == "Api" && Path.GetFileName(file) == "Program.cs")
                {
                    continue;
                }

                var content = File.ReadAllText(file);
                foreach (var violation in LayerScanHelper.FindViolations(layer, content))
                {
                    violations.Add($"{Path.GetRelativePath(root!, file)}: {violation}");
                }
            }
        }

        Assert.Empty(violations);                                           // T-27 point 2
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-003")]
    [Trait("Category", "ARC")]
    public void Namespaces_MatchLayerFolders()
    {
        var root = LayerScanHelper.FindSolutionRoot();
        Assert.NotNull(root!);
        var sourceRoot = Path.Combine(root!, "src", "OcrAcl.Api");

        var violations = new List<string>();
        foreach (var layer in new[] { "Api", "Application", "Infrastructure" })
        {
            var types = TypesInNamespace($"OcrAcl.{layer}")
                .Where(t => t.Namespace != "OcrAcl.Api.Middleware" || layer == "Api")
                .ToArray();
            Assert.NotEmpty(types);                                         // T-27 point 4

            var folder = Path.Combine(sourceRoot, layer);
            foreach (var type in types)
            {
                var expectedPrefix = $"OcrAcl.{layer}";
                if (!(type.Namespace ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    violations.Add(type.FullName ?? type.Name);
                }
            }

            Assert.True(Directory.Exists(folder));
        }

        Assert.Empty(violations);                                           // T-27 point 3
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-004")]
    [Trait("Category", "ARC")]
    public void SourceScanHelper_ReportsViolatingInput()
    {
        const string source = "using Microsoft.AspNetCore.Mvc;\nnamespace X;";

        var violations = LayerScanHelper.FindViolations("Application", source);

        Assert.Single(violations);                                          // T-27 point 4
        Assert.Contains("Microsoft.AspNetCore", violations[0]);             // T-27 point 4
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-005")]
    [Trait("Category", "ARC")]
    public void LayerRuleChecks_FindRealTypesAndFiles()
    {
        Assert.NotEmpty(TypesInNamespace("OcrAcl.Application"));             // T-27 point 4

        var root = LayerScanHelper.FindSolutionRoot();
        Assert.NotNull(root);                                               // T-27 point 2

        var sourceRoot = Path.Combine(root!, "src", "OcrAcl.Api");
        foreach (var layer in new[] { "Api", "Application", "Infrastructure" })
        {
            var files = Directory.GetFiles(Path.Combine(sourceRoot, layer), "*.cs", SearchOption.AllDirectories);
            Assert.NotEmpty(files);                                         // T-27 point 4
        }
    }
}
