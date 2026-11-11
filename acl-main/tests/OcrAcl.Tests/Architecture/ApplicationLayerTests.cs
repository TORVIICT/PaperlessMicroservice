using System.Reflection;
using System.Text.RegularExpressions;

namespace OcrAcl.Tests.Architecture;

/// <summary>
/// Architecture guard tests for the Application layer.
/// Ensures Application references no Api, Infrastructure, or ASP.NET Core types.
/// </summary>
public sealed class ApplicationLayerTests
{
    private static readonly string[] ForbiddenNamespaces =
    {
        "OcrAcl.Api",
        "OcrAcl.Infrastructure",
        "Microsoft.AspNetCore"
    };

    private static Assembly ApplicationAssembly => typeof(OcrAcl.Application.Models.OcrJob).Assembly;

    [Fact]
    [Trait("TestCaseId", "TC-ARC-010")]
    [Trait("Category", "ARC")]
    public void ApplicationTypes_DoNotReferenceForbiddenNamespaces()
    {
        var applicationTypes = GetApplicationTypes().ToArray();
        Assert.NotEmpty(applicationTypes); // T-27 point 4: at least one type exists

        var violations = new List<string>();
        foreach (var type in applicationTypes)
        {
            foreach (var referenced in GetReferencedTypes(type))
            {
                var ns = referenced.Namespace ?? string.Empty;
                if (ForbiddenNamespaces.Any(forbidden =>
                        ns == forbidden || ns.StartsWith(forbidden + ".", StringComparison.Ordinal)))
                {
                    violations.Add($"{type.FullName} -> {referenced.FullName}");
                }
            }
        }

        Assert.Empty(violations); // T-27 point 1: no forbidden references
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-011")]
    [Trait("Category", "ARC")]
    public void ApplicationSourceFiles_DoNotContainForbiddenUsings()
    {
        var root = FindSolutionRoot();
        Assert.NotNull(root); // T-27 point 2: solution root found

        var appRoot = Path.Combine(root!, "src", "OcrAcl.Api", "Application");
        Assert.True(Directory.Exists(appRoot));

        var files = Directory.GetFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();

        Assert.NotEmpty(files); // T-27 point 4: at least one file

        var violations = new List<string>();
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var violation in ScanForViolations("Application", content))
            {
                violations.Add($"{Path.GetRelativePath(root!, file)}: {violation}");
            }
        }

        Assert.Empty(violations); // T-27 point 2: no forbidden usings in source
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-012")]
    [Trait("Category", "ARC")]
    public void ApplicationNamespaces_MatchFolderStructure()
    {
        var types = GetApplicationTypes().ToArray();
        Assert.NotEmpty(types); // T-27 point 4

        var violations = new List<string>();
        foreach (var type in types)
        {
            var expectedPrefix = "OcrAcl.Application";
            if (!(type.Namespace ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                violations.Add(type.FullName ?? type.Name);
            }
        }

        Assert.Empty(violations); // T-27 point 3: namespace matches folder
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-013")]
    [Trait("Category", "ARC")]
    public void ScanHelper_DetectsForbiddenUsingInSyntheticInput()
    {
        const string source = "using Microsoft.AspNetCore.Mvc;\nnamespace OcrAcl.Application.Test;";

        var violations = ScanForViolations("Application", source).ToArray();

        Assert.Single(violations); // T-27 point 4: helper works
        Assert.Contains("Microsoft.AspNetCore", violations[0]);
    }

    [Fact]
    [Trait("TestCaseId", "TC-ARC-014")]
    [Trait("Category", "ARC")]
    public void ApplicationHasAtLeastOneTypeAndFile()
    {
        var types = GetApplicationTypes().ToArray();
        Assert.NotEmpty(types);

        var root = FindSolutionRoot();
        Assert.NotNull(root);
        var appRoot = Path.Combine(root!, "src", "OcrAcl.Api", "Application");
        var files = Directory.GetFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();
        Assert.NotEmpty(files);
    }

    private static IEnumerable<Type> GetApplicationTypes()
    {
        return ApplicationAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("OcrAcl.Application", StringComparison.Ordinal) ?? false);
    }

    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        var results = new HashSet<Type>();

        void Add(Type? candidate)
        {
            if (candidate is null) return;
            if (results.Add(candidate) && candidate.IsGenericType)
            {
                foreach (var arg in candidate.GetGenericArguments()) Add(arg);
            }
        }

        Add(type.BaseType);
        foreach (var iface in type.GetInterfaces()) Add(iface);

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            Add(field.FieldType);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            Add(prop.PropertyType);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Add(method.ReturnType);
            foreach (var p in method.GetParameters()) Add(p.ParameterType);
        }

        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (var p in ctor.GetParameters()) Add(p.ParameterType);
        }

        return results;
    }

    private static string? FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OcrAcl.slnx")) ||
                File.Exists(Path.Combine(directory.FullName, "OcrAcl.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private static IEnumerable<string> ScanForViolations(string layer, string source)
    {
        var forbidden = layer switch
        {
            "Application" => new[] { "OcrAcl.Api", "OcrAcl.Infrastructure", "Microsoft.AspNetCore" },
            _ => Array.Empty<string>()
        };

        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var token in forbidden)
            {
                if (Regex.IsMatch(line, $@"\busing\s+{Regex.Escape(token)}\b") ||
                    Regex.IsMatch(line, $@"\b{Regex.Escape(token)}\."))
                {
                    yield return $"line {i + 1}: {token}";
                }
            }
        }
    }
}