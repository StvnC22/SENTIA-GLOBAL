using System.Text.RegularExpressions;
using NUnit.Framework;
using Shouldly;

namespace ArchitectureTests;

public class TestConventionsTests
{
    private static readonly HashSet<string> RelativePathsExemptFromSendAsyncMixingCheck = [];

    [Test]
    public void FunctionalTests_QueriesNoDebenEnviarCommands_Y_CommandsNoDebenEnviarQueries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var functionalTestsRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "Application.FunctionalTests"
        );

        var testFiles = Directory
            .GetFiles(functionalTestsRoot, "*Tests.cs", SearchOption.AllDirectories)
            .Where(path =>
                path.Contains(
                    $"{Path.DirectorySeparatorChar}Commands{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
                || path.Contains(
                    $"{Path.DirectorySeparatorChar}Queries{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            )
            .ToArray();

        var violations = new List<string>();

        foreach (var filePath in testFiles)
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, filePath);
            if (RelativePathsExemptFromSendAsyncMixingCheck.Contains(relativePath))
            {
                continue;
            }

            var content = File.ReadAllText(filePath);
            var variableKinds = GetVariableKinds(content);

            var sendArguments = Regex
                .Matches(
                    content,
                    @"(?:TestApp\.)?SendAsync\s*\(\s*(?<arg>[^\),]+)",
                    RegexOptions.CultureInvariant
                )
                .Select(match => match.Groups["arg"].Value.Trim())
                .ToArray();

            var isQueryTest = relativePath.Contains(
                $"{Path.DirectorySeparatorChar}Queries{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal
            );
            var isCommandTest = relativePath.Contains(
                $"{Path.DirectorySeparatorChar}Commands{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal
            );

            foreach (var argument in sendArguments)
            {
                if (IsCommentOrLiteral(argument))
                {
                    continue;
                }

                if (isQueryTest && IsCommandArgument(argument, variableKinds))
                {
                    violations.Add($"{relativePath} => SendAsync({argument}) en test de Queries.");
                }

                if (isCommandTest && IsQueryArgument(argument, variableKinds))
                {
                    violations.Add($"{relativePath} => SendAsync({argument}) en test de Commands.");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Se detectó mezcla de Commands/Queries en tests funcionales usando SendAsync.\n"
                + string.Join(Environment.NewLine, violations)
        );
    }

    private static Dictionary<string, string> GetVariableKinds(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        var varDeclarationMatches = Regex.Matches(
            content,
            @"\bvar\s+(?<name>\w+)\s*=\s*new\s+(?<type>\w+)\b",
            RegexOptions.CultureInvariant
        );

        foreach (Match match in varDeclarationMatches)
        {
            var variableName = match.Groups["name"].Value;
            var typeName = match.Groups["type"].Value;

            if (typeName.EndsWith("Command", StringComparison.Ordinal))
            {
                result[variableName] = "Command";
            }
            else if (typeName.EndsWith("Query", StringComparison.Ordinal))
            {
                result[variableName] = "Query";
            }
        }

        var explicitDeclarationMatches = Regex.Matches(
            content,
            @"\b(?<type>\w+(?:Command|Query))\s+(?<name>\w+)\s*=",
            RegexOptions.CultureInvariant
        );

        foreach (Match match in explicitDeclarationMatches)
        {
            var variableName = match.Groups["name"].Value;
            var typeName = match.Groups["type"].Value;

            if (typeName.EndsWith("Command", StringComparison.Ordinal))
            {
                result[variableName] = "Command";
            }
            else if (typeName.EndsWith("Query", StringComparison.Ordinal))
            {
                result[variableName] = "Query";
            }
        }

        return result;
    }

    private static bool IsCommandArgument(
        string argument,
        IReadOnlyDictionary<string, string> variableKinds
    )
    {
        if (Regex.IsMatch(argument, @"^new\s+\w*Command\b", RegexOptions.CultureInvariant))
        {
            return true;
        }

        if (variableKinds.TryGetValue(argument, out var kind))
        {
            return kind == "Command";
        }

        return argument.EndsWith("Command", StringComparison.Ordinal);
    }

    private static bool IsQueryArgument(
        string argument,
        IReadOnlyDictionary<string, string> variableKinds
    )
    {
        if (Regex.IsMatch(argument, @"^new\s+\w*Query\b", RegexOptions.CultureInvariant))
        {
            return true;
        }

        if (variableKinds.TryGetValue(argument, out var kind))
        {
            return kind == "Query";
        }

        return argument.EndsWith("Query", StringComparison.Ordinal);
    }

    private static bool IsCommentOrLiteral(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.StartsWith("\"", StringComparison.Ordinal)
            || text.StartsWith("'", StringComparison.Ordinal)
            || text.StartsWith("//", StringComparison.Ordinal)
            || text.StartsWith("/*", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionFile = Path.Combine(currentDirectory.FullName, "AnalisisSentimiento.slnx");
            if (File.Exists(solutionFile))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encontró la raíz del repositorio (AnalisisSentimiento.slnx)."
        );
    }
}
