using lazy_light_requests_gate.middleware;
using Serilog;

Console.Title = "slow & light dynamic gate";

var builder = WebApplication.CreateBuilder(args);

LoggingConfiguration.ConfigureLogging(builder);

ConfigureServices(builder);

var app = builder.Build();

try
{
	// ��������� ������������� ����� (����� ������������������ ������)
	var gateConfigurator = app.Services.GetRequiredService<GateConfiguration>();
	var (httpUrl, httpsUrl) = await gateConfigurator.ConfigureDynamicGateAsync(args, builder);

	// ��������� ��������� ����������
	ConfigureApp(app, httpUrl, httpsUrl);

	// ���������
	await app.RunAsync();
}
catch (Exception ex)
{
	Log.Fatal(ex, "����������� ������ ��� ������� ����������");
	throw;
}
finally
{
	Log.CloseAndFlush();
}

static void ConfigureServices(WebApplicationBuilder builder)
{

	var configuration = builder.Configuration;

	var services = builder.Services;

	services.AddControllers();

	services.AddCommonServices();
	services.AddHttpServices();
	services.AddRabbitMqServices(configuration);
	services.AddMessageServingServices();
	services.AddMongoDbServices(configuration);
	services.AddMongoDbRepositoriesServices(configuration);
	services.AddValidationServices();
	services.AddHostedServices();
	services.AddHeadersServices();

	// ������������ GateConfiguration
	services.AddSingleton<GateConfiguration>();
}

static void ConfigureApp(WebApplication app, string httpUrl, string httpsUrl)
{
	app.Urls.Add(httpUrl);
	app.Urls.Add(httpsUrl);
	Log.Information($"Middleware: ������������ ���� ������� � ��������� ������� �� ��������� ������: {httpUrl} � {httpsUrl}");

	app.UseSerilogRequestLogging();

	app.UseCors(cors => cors
		.AllowAnyOrigin()
		.AllowAnyMethod()
		.AllowAnyHeader());

	app.MapControllers();
}
