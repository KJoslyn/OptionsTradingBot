using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;

namespace LottoXService
{
    public class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            try
            {
                Log.Information("LottoX Application Starting Up");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                // TODO: This will actually catch any error thrown in the worker. Need to read about services and how they work.
                Log.Fatal(ex, "The application failed to start correctly."); // This message is misleading.
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostcontext, services) =>
                {
                    services.AddHostedService<Worker>();
                });
    }
}
