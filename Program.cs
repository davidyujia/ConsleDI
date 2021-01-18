using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace ConsoleTest
{
    public class Test
    {
        private readonly ILogger<Test> _logger;

        public Test(ILogger<Test> logger)
        {
            _logger = logger;
        }

        public void Start()
        {
            _logger.LogInformation("Test");
        }
    }

    class Program
    {
        private static void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<Test>();
        }

        private static void Main(string[] args)
        {
            using var services = new ConsoleServiceCollection(ConfigureServices);

            var test = services.GetService<Test>();

            test.Start();
        }
    }

    public static class Extensions
    {
        public static IDisposable CreateLoggerScope(this Microsoft.Extensions.Logging.ILogger logger, params string[] ids)
        {
            var idList = new List<string>(ids);

            return logger.BeginScope(new[] { new KeyValuePair<string, object>("ScopeId", string.Join("_", idList)) });
        }
    }

    /// <summary>
    /// Console 用 DI
    /// </summary>
    public sealed class ConsoleServiceCollection : IDisposable
    {
        private readonly IServiceCollection _services;
        private Lazy<IServiceProvider> Provider => new Lazy<IServiceProvider>(() => _services.BuildServiceProvider());

        private static readonly Lazy<string> BasePath = new Lazy<string>(() =>
        {
            using var processModule = Process.GetCurrentProcess().MainModule;
            return Path.GetDirectoryName(processModule?.FileName);
        });

        public ConsoleServiceCollection(Action<IServiceCollection, IConfiguration> addServices, bool writeToConsole = true)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(BasePath.Value)
                .AddJsonFile("appSettings.json", true)
                .AddJsonFile($"appSettings.{environment}.json", true)
                .Build();

            _services = new ServiceCollection();

            _services.AddLogging(configure => SetLoggingBuilder(configure, config, writeToConsole));

            addServices?.Invoke(_services, config);

            var logger = _services.BuildServiceProvider().GetService<ILogger<ConsoleServiceCollection>>();
            logger.LogInformation($"Environment: {environment}");
        }

        /// <summary>
        /// Sets the logging builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="writeToConsole">if set to <c>true</c> [write console].</param>
        private static void SetLoggingBuilder(ILoggingBuilder builder, IConfiguration config, bool writeToConsole)
        {
            if (writeToConsole)
            {
                builder.AddConsole();
            }

            builder.AddConfiguration(config.GetSection("Logging"));
            LogManager.Configuration = new NLogLoggingConfiguration(config.GetSection("NLog"));
            builder.AddProvider(new NLogLoggerProvider());
        }

        public string ExecutePath => BasePath.Value;

        /// <summary>
        /// Get service
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetService<T>()
        {
            return Provider.Value.GetRequiredService<T>();
        }

        public IServiceScope CreateScope()
        {
            return Provider.Value.CreateScope();
        }

        public void Dispose()
        {
            LogManager.Shutdown();
        }
    }
}
