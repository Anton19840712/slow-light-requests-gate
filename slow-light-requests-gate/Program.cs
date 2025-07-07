using lazy_light_requests_gate.core.application.configuration;
using Serilog;

Console.Title = "slow & light rest http protocol dynamic gate";
var instanceId = $"{Environment.MachineName}_{Guid.NewGuid()}";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"\nЗапускается экземпляр DynamicGate ID: {instanceId}\n");
Console.ResetColor();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

try
{
	var startup = new ApplicationStartup();
	await startup.RunAsync(args, instanceId);
}
catch (Exception ex)
{
	Log.Fatal(ex, "Критическая ошибка при запуске приложения");
	throw;
}
finally
{
	Log.CloseAndFlush();
}