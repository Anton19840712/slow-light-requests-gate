using lazy_light_requests_gate.temp.apptypeswitcher;
using Serilog;

namespace lazy_light_requests_gate.core.application.configuration
{
	public class ApplicationConfigurator
	{
		public void ConfigureApp(WebApplication app, string httpUrl, string httpsUrl)
		{
			try
			{
				ConfigureUrls(app, httpUrl, httpsUrl);
				ConfigureMiddleware(app);
				ConfigureRouting(app);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Ошибка конфигурации приложения: {ex.Message}");
				Console.WriteLine($"StackTrace: {ex.StackTrace}");
				throw;
			}
		}

		private void ConfigureUrls(WebApplication app, string httpUrl, string httpsUrl)
		{
			app.Urls.Add(httpUrl);
			app.Urls.Add(httpsUrl);
			Log.Information($"Middleware: шлюз работает на адресах: {httpUrl} и {httpsUrl}");
		}

		private void ConfigureMiddleware(WebApplication app)
		{
			try
			{
				// 1. Логирование запросов (первым)
				app.UseSerilogRequestLogging();

				// 2. CORS
				app.UseCors(cors => cors
					.AllowAnyOrigin()
					.AllowAnyMethod()
					.AllowAnyHeader());

				// 3. Аутентификация
				app.UseAuthentication();

				app.UseMiddleware<ApplicationTypeLoggingMiddleware>();

				// 4. Swagger (ВАЖНО: между Authentication и Authorization!)
				ConfigureSwagger(app);

				// 5. Авторизация (после Swagger!)
				app.UseAuthorization();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Ошибка настройки middleware: {ex.Message}");
				throw;
			}
		}

		private void ConfigureSwagger(WebApplication app)
		{
			try
			{
				// Всегда включаем Swagger (уберем проверку Development для диагностики)
				app.UseSwagger();
				app.UseSwaggerUI(c =>
				{
					c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic Gate API v1");
					c.RoutePrefix = string.Empty; // Доступен на корне "/"
					c.DocumentTitle = "Dynamic Gate API";
					c.DisplayRequestDuration(); // Показывать время выполнения запросов
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Ошибка настройки Swagger: {ex.Message}");
				Console.WriteLine($"StackTrace: {ex.StackTrace}");
				throw;
			}
		}

		private void ConfigureRouting(WebApplication app)
		{
			try
			{
				app.MapControllers();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Ошибка настройки маршрутизации: {ex.Message}");
				throw;
			}
		}
	}
}