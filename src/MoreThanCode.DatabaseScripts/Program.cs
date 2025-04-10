// See https://aka.ms/new-console-template for more information

using System.Reflection;
using DbUp;

try
{
    var connectionString = args.FirstOrDefault() ?? throw new Exception("No connectionstring provided");

    var upgrader = DeployChanges.To.SqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();

    if (!result.Successful)
    {
        WriteError(result.Error);
        return -1;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Success!");
    Console.ResetColor();
}
catch (Exception ex)
{
    WriteError(ex);
}

return 0;

void WriteError(Exception error)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(error);
    Console.ResetColor();
#if DEBUG
    Console.ReadLine();
#endif
}