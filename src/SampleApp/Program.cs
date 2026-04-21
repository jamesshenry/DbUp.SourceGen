using DbUp;
using DbUp.Engine;
using SampleApp;

[assembly: DbUpGenerateScripts]

Console.WriteLine("Starting AOT Migration Test...");

// This should use the Source Generated extension method
var upgrader = DeployChanges
    .To.SqliteDatabase("Data Source=:memory:")
    .WithGeneratedScripts()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();

if (result.Successful)
{
    Console.WriteLine("Success!");
}
else
{
    Console.WriteLine("Failed: " + result.Error);
    Environment.Exit(1);
}

public class TestScript : IScript
{
    public string ProvideScript(Func<System.Data.IDbCommand> dbCommandFactory)
    {
        return "SELECT 1";
    }
}

[DbUpScript(ScriptType = DbUpScriptType.RunAlways, RunGroupOrder = 1)]
public class PreDeploymentCheck : IScript
{
    public string ProvideScript(Func<System.Data.IDbCommand> dbCommandFactory)
    {
        return "SELECT 'pre-deploy check'";
    }
}

[DbUpScript(RunGroupOrder = 3)]
public class PostDeploymentSeed : IScript
{
    public string ProvideScript(Func<System.Data.IDbCommand> dbCommandFactory)
    {
        return "SELECT 'post-deploy seed'";
    }
}
