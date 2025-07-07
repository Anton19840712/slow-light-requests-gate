using Serilog;

namespace lazy_light_requests_gate.infrastructure.startup
{
	public class ApplicationConfigurator
	{
		public void ConfigureApp(WebApplication app, string httpUrl, string httpsUrl)
		{
			try
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Начинаем конфигурацию приложения...");

				ConfigureUrls(app, httpUrl, httpsUrl);
				ConfigureMiddleware(app);
				ConfigureRouting(app);
				DiagnoseRoutes(app);

				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Конфигурация приложения завершена успешно");
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
			Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] URLs настроены: {httpUrl}, {httpsUrl}");
		}

		private void ConfigureMiddleware(WebApplication app)
		{
			try
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Настройка middleware...");

				// 1. Логирование запросов (первым)
				app.UseSerilogRequestLogging();
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Serilog middleware добавлен");

				// 2. CORS
				app.UseCors(cors => cors
					.AllowAnyOrigin()
					.AllowAnyMethod()
					.AllowAnyHeader());
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] CORS middleware добавлен");

				// 3. Аутентификация
				app.UseAuthentication();
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Authentication middleware добавлен");

				// 4. Swagger (ВАЖНО: между Authentication и Authorization!)
				ConfigureSwagger(app);

				// 5. Авторизация (после Swagger!)
				app.UseAuthorization();
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Authorization middleware добавлен");

				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Все middleware настроены успешно");
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
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Настройка маршрутизации...");
				app.MapControllers();
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Контроллеры подключены");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Ошибка настройки маршрутизации: {ex.Message}");
				throw;
			}
		}

		private void DiagnoseRoutes(WebApplication app)
		{
			try
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Диагностика маршрутов...");
				var endpoints = app.Services.GetRequiredService<EndpointDataSource>();
				int routeCount = 0;

				foreach (var endpoint in endpoints.Endpoints)
				{
					if (endpoint is RouteEndpoint routeEndpoint)
					{
						Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ROUTE] Найден маршрут: {routeEndpoint.RoutePattern.RawText}");
						routeCount++;
					}
				}

				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CONFIG] Всего найдено маршрутов: {routeCount}");

				if (routeCount == 0)
				{
					Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WARNING] Маршруты не найдены! Проверьте регистрацию контроллеров.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Ошибка диагностики маршрутов: {ex.Message}");
				// Не бросаем исключение, это не критично
			}
		}
	}
}