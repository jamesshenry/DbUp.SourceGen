using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DbUp.SourceGen.Tests;

internal static class GeneratorTestHelper
{
    private static readonly string IScriptInterface =
        @"
namespace DbUp.Engine
{
    public interface IScript
    {
        string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory);
    }
}
";

    private static readonly string DbUpStubs =
        @"
namespace DbUp.Builder
{
    public class UpgradeEngineBuilder
    {
        public void Configure(System.Action<object> action) { }
    }
}
namespace DbUp.Engine
{
    public class SqlScriptOptions
    {
        public DbUp.Support.ScriptType ScriptType { get; set; }
        public int RunGroupOrder { get; set; }
    }
}
namespace DbUp.Support
{
    public enum ScriptType { RunOnce = 0, RunAlways = 1 }
}
public static class StandardExtensions
{
    public static DbUp.Builder.UpgradeEngineBuilder WithScript(this DbUp.Builder.UpgradeEngineBuilder builder, string name, DbUp.Engine.IScript script) => builder;
    public static DbUp.Builder.UpgradeEngineBuilder WithScript(this DbUp.Builder.UpgradeEngineBuilder builder, string name, DbUp.Engine.IScript script, DbUp.Engine.SqlScriptOptions options) => builder;
}
";

    public static (GeneratorRunResult Result, Compilation OutputCompilation) RunGenerator(
        string source
    ) => RunGeneratorWithAssemblyName(source, "TestAssembly");

    public static (
        GeneratorRunResult Result,
        Compilation OutputCompilation
    ) RunGeneratorWithAssemblyName(string source, string assemblyName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var iScriptTree = CSharpSyntaxTree.ParseText(IScriptInterface);
        var stubsTree = CSharpSyntaxTree.ParseText(DbUpStubs);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Data.IDbCommand).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        // Add runtime assembly refs
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var additionalRefs = new[]
        {
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")
            ),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(runtimeDir, "System.Data.Common.dll")
            ),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree, iScriptTree, stubsTree],
            references.Concat(additionalRefs),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new Generators.DbUpScriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _
        );

        var result = driver.GetRunResult().Results[0];
        return (result, outputCompilation);
    }

    public static string? GetGeneratedSource(
        GeneratorRunResult result,
        string hintName = "DbUpGeneratedScripts.g.cs"
    )
    {
        return result
            .GeneratedSources.FirstOrDefault(s => s.HintName == hintName)
            .SourceText?.ToString();
    }

    public static IEnumerable<Diagnostic> GetDiagnostics(GeneratorRunResult result, string id)
    {
        return result.Diagnostics.Where(d => d.Id == id);
    }

    public static void AssertNoCompilationErrors(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Any())
        {
            throw new Exception(
                "Compilation errors found: " + string.Join(", ", errors.Select(e => e.GetMessage()))
            );
        }
    }
}
