using Microsoft.Extensions.DependencyInjection;
using NoFences.Persistence;
using NoFences.Services;
using NoFences.Model;
using System;

namespace NoFences.Core
{
    /// <summary>
    /// Dependency Injection container configuration
    /// </summary>
    public static class DependencyInjection
    {
        private static ServiceProvider _serviceProvider;

        /// <summary>
        /// Configures and builds the service provider
        /// </summary>
        public static void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core Services
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            
            // Persistence (requires ILoggingService)
            services.AddSingleton<IPersistenceService, JsonPersistenceService>();
            
            // Business Logic
            services.AddSingleton<IFenceService, FenceService>();
            services.AddSingleton<ITrayIconManager, TrayIconManager>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<ISmartSorterService, SmartSorterService>();
            
            // Innovative Features
            services.AddSingleton<IHistoryService, HistoryService>();
            services.AddSingleton<ContextManager>();

            // Build the service provider
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Gets a service from the container
        /// </summary>
        public static T GetService<T>()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider not configured. Call ConfigureServices first.");
            }

            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets a required service from the container
        /// </summary>
        public static T GetRequiredService<T>()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider not configured. Call ConfigureServices first.");
            }

            return _serviceProvider.GetRequiredService<T>();
        }

        public static IServiceProvider GetServiceProvider()
        {
            return _serviceProvider;
        }

        /// <summary>
        /// Disposes the service provider
        /// </summary>
        public static void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }
    }
}
