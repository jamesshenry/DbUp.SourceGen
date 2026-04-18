using Microsoft.CodeAnalysis;

namespace DbUp.SourceGen.Tests;

public class DbUpScriptGeneratorTests
{
    [Test]
    public async Task No_marker_attribute_generates_nothing()
    {
        var source =
            @"
using DbUp.Engine;

public class MyScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);

        await Assert.That(generated).IsNull();
    }

    [Test]
    public async Task Marker_attribute_with_one_script_generates_extension()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class MyScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("WithGeneratedScripts_TestAssembly");
        await Assert.That(generated).Contains("new MyScript()");
        await Assert.That(generated).Contains("\"MyScript.cs\"");
    }

    [Test]
    public async Task Scripts_sorted_alphabetically_by_fully_qualified_name()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

namespace Z
{
    public class First : IScript
    {
        public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
    }
}

namespace A
{
    public class Second : IScript
    {
        public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 2"";
    }
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();

        var indexA = generated.IndexOf("A.Second");
        var indexZ = generated.IndexOf("Z.First");
        await Assert
            .That(indexA)
            .IsLessThan(indexZ)
            .Because("A.Second should appear before Z.First (alphabetical sort)");
    }

    [Test]
    public async Task Abstract_class_is_skipped()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public abstract class BaseScript : IScript
{
    public abstract string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory);
}

public class ConcreteScript : BaseScript
{
    public override string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).DoesNotContain("BaseScript");
        await Assert.That(generated).Contains("ConcreteScript");
    }

    [Test]
    public async Task No_parameterless_constructor_reports_DBUP001()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class BadScript : IScript
{
    public BadScript(string connectionString) { }
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var diags = GeneratorTestHelper.GetDiagnostics(result, "DBUP001").ToList();
        await Assert.That(diags).Count().IsEqualTo(1);
        await Assert.That(diags[0].GetMessage()).Contains("BadScript");
    }

    [Test]
    public async Task Marker_with_no_scripts_reports_DBUP002()
    {
        var source =
            @"
using DbUp;

[assembly: DbUpGenerateScripts]

public class NotAScript { }
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var diags = GeneratorTestHelper.GetDiagnostics(result, "DBUP002").ToList();
        await Assert.That(diags).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DbUpScriptAttribute_sets_RunAlways()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

[DbUpScript(ScriptType = DbUpScriptType.RunAlways)]
public class AlwaysScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("ScriptType.RunAlways");
    }

    [Test]
    public async Task DbUpScriptAttribute_sets_RunGroupOrder()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

[DbUpScript(RunGroupOrder = 5)]
public class OrderedScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("RunGroupOrder = 5");
    }

    [Test]
    public async Task Default_options_omit_SqlScriptOptions()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class SimpleScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).DoesNotContain("SqlScriptOptions");
    }

    [Test]
    public async Task Generated_class_name_uses_assembly_name()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class MyScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("DbUpGeneratedScripts_TestAssembly");
    }

    [Test]
    public async Task Namespaced_script_uses_fully_qualified_name()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

namespace MyApp.Migrations
{
    public class Migration001 : IScript
    {
        public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
    }
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("\"MyApp.Migrations.Migration001.cs\"");
        await Assert.That(generated).Contains("new MyApp.Migrations.Migration001()");
    }

    [Test]
    public async Task Both_script_options_combined()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

[DbUpScript(ScriptType = DbUpScriptType.RunAlways, RunGroupOrder = 3)]
public class PostDeployScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("ScriptType.RunAlways");
        await Assert.That(generated).Contains("RunGroupOrder = 3");
    }

    [Test]
    public async Task Attributes_always_emitted_via_post_init()
    {
        var source = "public class Empty { }";
        var (result, _) = GeneratorTestHelper.RunGenerator(source);
        var attrSource = GeneratorTestHelper.GetGeneratedSource(result, "DbUpAttributes.g.cs");
        await Assert.That(attrSource).IsNotNull();
        await Assert.That(attrSource).Contains("DbUpGenerateScriptsAttribute");
        await Assert.That(attrSource).Contains("DbUpScriptAttribute");
        await Assert.That(attrSource).Contains("DbUpScriptType");
    }

    [Test]
    public async Task Leading_digit_assembly_name_is_sanitized()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class MyScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, output) = GeneratorTestHelper.RunGeneratorWithAssemblyName(
            source,
            "123Project"
        );
        GeneratorTestHelper.AssertNoCompilationErrors(output);

        var generated = GeneratorTestHelper.GetGeneratedSource(result);

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("DbUpGeneratedScripts__123Project");
        await Assert.That(generated).Contains("WithGeneratedScripts__123Project");
    }

    [Test]
    public async Task Multiple_constructors_succeeds_if_one_is_parameterless()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class MultiCtorScript : IScript
{
    public MultiCtorScript() { }
    public MultiCtorScript(string name) { }
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, output) = GeneratorTestHelper.RunGenerator(source);
        GeneratorTestHelper.AssertNoCompilationErrors(output);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).Contains("new MultiCtorScript()");
    }

    [Test]
    public async Task Nested_class_script_generates_correct_name()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

public class Outer
{
    public class Inner : IScript
    {
        public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
    }
}
";
        var (result, output) = GeneratorTestHelper.RunGenerator(source);
        GeneratorTestHelper.AssertNoCompilationErrors(output);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).Contains("new Outer.Inner()");
    }

    [Test]
    public async Task Internal_class_is_included()
    {
        var source =
            @"
using DbUp;
using DbUp.Engine;

[assembly: DbUpGenerateScripts]

internal class InternalScript : IScript
{
    public string ProvideScript(System.Func<System.Data.IDbCommand> dbCommandFactory) => ""SELECT 1"";
}
";
        var (result, output) = GeneratorTestHelper.RunGenerator(source);
        GeneratorTestHelper.AssertNoCompilationErrors(output);
        var generated = GeneratorTestHelper.GetGeneratedSource(result);
        await Assert.That(generated).Contains("new InternalScript()");
    }
}
