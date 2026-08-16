using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using NoFences.Core;
using NoFences.Migrations;
using NoFences.Model;
using NoFences.Persistence;
using NoFences.Services;
using NoFences.Win32;

namespace NoFences
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.TypeResolve += (sender, args) =>
            {
                try
                {
                    if (args.Name.StartsWith("System.Resources.Extensions.DeserializingResourceReader"))
                    {
                        return typeof(System.Resources.Extensions.DeserializingResourceReader).Assembly;
                    }
                }
                catch { }
                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    var req = new AssemblyName(args.Name);
                    if (req.Name == "System.Resources.Extensions")
                    {
                        return typeof(System.Resources.Extensions.DeserializingResourceReader).Assembly;
                    }
                    if (req.Name == "System.Runtime.CompilerServices.Unsafe")
                    {
                        return typeof(System.Runtime.CompilerServices.Unsafe).Assembly;
                    }

                    foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (loaded.GetName().Name.Equals(req.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return loaded;
                        }
                    }

                    var appDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
                    if (!string.IsNullOrEmpty(appDir))
                    {
                        var candidate = Path.Combine(appDir, req.Name + ".dll");
                        if (File.Exists(candidate))
                        {
                            return Assembly.LoadFrom(candidate);
                        }
                    }
                }
                catch { }
                return null;
            };

            // Single instance check
            var currentProc = Process.GetCurrentProcess();
            var existingProcs = Process.GetProcessesByName(currentProc.ProcessName);
            if (existingProcs.Length > 1)
            {
                MessageBox.Show(
                    "Universe is already running.",
                    "Universe",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            try
            {
                // Configure Windows Forms FIRST
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Configure dependency injection
                DependencyInjection.ConfigureServices();

                // Get services from DI container
                var loggingService = DependencyInjection.GetRequiredService<ILoggingService>();
                var errorHandlingService = DependencyInjection.GetRequiredService<IErrorHandlingService>();
                var fenceService = DependencyInjection.GetRequiredService<IFenceService>();
                var trayIconManager = DependencyInjection.GetRequiredService<ITrayIconManager>();

                loggingService.LogInfo("Universe starting...");

                try
                {
                    // Initialize error handling
                    errorHandlingService.Initialize();

                    // Initialize update checker (silent on startup)
                    try
                    {
                        var updateService = DependencyInjection.GetRequiredService<IUpdateService>();
                        updateService.CheckForUpdates(true);
                    }
                    catch (Exception ex)
                    {
                        loggingService.LogWarning($"Update check failed: {ex.Message}");
                    }

                    // Enable dark mode for context menus
                    WindowUtil.SetPreferredAppMode(1);

                    // Load Language
                    var config = AppConfig.Load();
                    LocalizationManager.CurrentLanguage = config.Language == "Spanish" ? LocalizationManager.Language.Spanish : LocalizationManager.Language.English;

                    // Check for migration from XML to JSON
                    var migrator = new XmlToJsonMigrator(loggingService);
                    if (migrator.IsMigrationNeeded())
                    {
                        loggingService.LogInfo("Migration needed from XML to JSON format");
                        
                        var result = MessageBox.Show(
                            "Universe needs to upgrade your fence data to a new format.\n\n" +
                            "Your existing fences will be preserved. Do you want to continue?",
                            "Data Migration Required - Universe",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );

                        if (result == DialogResult.Yes)
                        {
                            try
                            {
                                migrator.BackupOldData();
                                var migratedFences = migrator.MigrateAllFences();
                                var persistenceService = DependencyInjection.GetRequiredService<IPersistenceService>();
                                foreach (var fence in migratedFences)
                                {
                                    persistenceService.SaveFence(fence);
                                }
                                migrator.CleanupOldFiles();

                                MessageBox.Show(
                                    $"Migration completed successfully!\n\nMigrated {migratedFences.Count} fences.",
                                    "Migration Complete - Universe",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information
                                );
                            }
                            catch (Exception ex)
                            {
                                errorHandlingService.HandleException(ex, "Migration Error");
                                errorHandlingService.ShowErrorDialog("Failed to migrate data. Please check logs for details.", ex);
                            }
                        }
                        else
                        {
                            loggingService.LogInfo("User cancelled migration");
                            return;
                        }
                    }

                    // Load existing fences
                    fenceService.LoadFences();

                    // Create first fence if none exist
                    if (fenceService.GetAllFences().Count == 0)
                    {
                        loggingService.LogInfo("No fences found, creating first fence");
                        fenceService.CreateFence("First fence");
                    }

                    loggingService.LogInfo("Universe started successfully");

                    // Run the application with UniverseApplicationContext
                    var smartSorterService = DependencyInjection.GetRequiredService<ISmartSorterService>();
                    Application.Run(new NoFences.UI.UniverseApplicationContext(trayIconManager, loggingService, smartSorterService));
                }
                catch (Exception ex)
                {
                    errorHandlingService.HandleException(ex, "Fatal Error");
                    errorHandlingService.ShowErrorDialog("A fatal error occurred. The application will now close.", ex);
                }
                finally
                {
                    loggingService.LogInfo("Universe shutting down");
                    DependencyInjection.Dispose();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization error: {ex.Message}", "Universe Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
