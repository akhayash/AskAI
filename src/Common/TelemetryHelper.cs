using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Common;

/// <summary>
/// ヘルパークラス: OpenTelemetry の設定とロガーの作成を共通化
/// </summary>
public static class TelemetryHelper
{
    /// <summary>
    /// OpenTelemetry を設定して LoggerFactory を作成します
    /// </summary>
    /// <param name="serviceName">サービス名</param>
    /// <param name="configuration">設定</param>
    /// <returns>LoggerFactory と TracerProvider のタプル</returns>
    public static (ILoggerFactory LoggerFactory, TracerProvider TracerProvider, ActivitySource ActivitySource) SetupTelemetry(
        string serviceName, 
        IConfiguration configuration)
    {
        var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        // 空文字列の場合もデフォルト値を使用
        if (string.IsNullOrEmpty(otlpEndpoint))
        {
            otlpEndpoint = "http://localhost:4317";
        }

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName));
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;

                options.AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = new Uri(otlpEndpoint);
                });

                options.AddConsoleExporter();
            });
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });

            builder.SetMinimumLevel(LogLevel.Information);
        });

        var activitySource = new ActivitySource(serviceName);

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName))
            .AddSource(serviceName)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(exporterOptions =>
            {
                exporterOptions.Endpoint = new Uri(otlpEndpoint);
            })
            .AddConsoleExporter()
            .Build();

        var logger = loggerFactory.CreateLogger(serviceName);
        logger.LogInformation("=== アプリケーション起動 ===");
        logger.LogInformation("テレメトリ設定: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            logger.LogInformation("Application Insights 接続文字列が設定されています");
        }

        return (loggerFactory, tracerProvider, activitySource);
    }

    /// <summary>
    /// 設定を読み込みます
    /// </summary>
    /// <returns>IConfiguration</returns>
    public static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
