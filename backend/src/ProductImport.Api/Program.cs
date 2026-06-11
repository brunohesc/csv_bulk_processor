using Microsoft.EntityFrameworkCore;
using ProductImport.Application.Settings;
using ProductImport.Infrastructure.Background;
using ProductImport.Infrastructure.Data;
using ProductImport.Infrastructure.Repositories;
using ProductImport.Infrastructure.Services;
using ProductImport.Api.Hubs;
using ProductImport.Api.Services;
using ProductImport.Core.Interfaces;
using Serilog;
using ProductImport.Infrastructure;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ProductImport")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ImportJobId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/product-import-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ImportJobId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting ProductImport API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Explicitly set configuration base path to ensure appsettings.json is found
    var appDomainBase = AppDomain.CurrentDomain.BaseDirectory;
    var configPath = Path.GetDirectoryName(appDomainBase) ?? appDomainBase;
    builder.Configuration.SetBasePath(configPath)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();

    Log.Information("Configuration base path: {ConfigPath}", configPath);
    Log.Information("appsettings.json path: {AppSettingsPath}", Path.Combine(configPath, "appsettings.json"));

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddSignalR();

    // Add Swagger (Development only)
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    // Configure AppSettings
    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
    builder.Services.Configure<ImportSettings>(builder.Configuration.GetSection("AppSettings:Import"));
    builder.Services.Configure<CurrencySettings>(builder.Configuration.GetSection("AppSettings:Currency"));

    // Register HttpClient for ExchangeRateService
    builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>();

    // Register Infrastructure services
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseNpgsql(connectionString);
    });

    builder.Services.AddScoped<IProductRepository, ProductRepository>();
    builder.Services.AddScoped<IImportJobRepository, ImportJobRepository>();
    builder.Services.AddScoped<ProductImport.Infrastructure.Services.CsvProcessor>();
    builder.Services.AddScoped<IImportProgressNotifier, SignalRImportProgressNotifier>();

    // Register BackgroundService
    builder.Services.AddSingleton<ImportProcessingService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ImportProcessingService>());

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseSerilogRequestLogging();

    // Enable Swagger (Development only)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Import API v1");
        });
    }

    app.UseCors("AllowAll");
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<ImportProgressHub>("/hubs/importProgress");

    Log.Information("ProductImport API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
