using NoFences.Model;
using NoFences.Core;
using NoFences.Services;
using NoFences.Migrations;
using System;
using System.Threading;
using System.Windows.Forms;
using NoFences.Win32;
using NoFences.Persistence;

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
            using (var mutex = new Mutex(true, "Universe_App", out var createdNew))
            {
                if (createdNew)
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

                        // Initialize Tray Icon (Moved to HiddenMainForm)
                        // trayIconManager.Initialize();

                        // Check for updates silently
                        var updateService = DependencyInjection.GetRequiredService<IUpdateService>();
                        // Run in background to not block startup
                        System.Threading.Tasks.Task.Run(() => updateService.CheckForUpdates(true));

                        // Enable dark mode for context menus
                        WindowUtil.SetPreferredAppMode(1);

                        // Load Language
                        var config = NoFences.Model.AppConfig.Load();
                        NoFences.Services.LocalizationManager.CurrentLanguage = config.Language == "Spanish" ? NoFences.Services.LocalizationManager.Language.Spanish : NoFences.Services.LocalizationManager.Language.English;

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
                        if (Application.OpenForms.Count == 0)
                        {
                            loggingService.LogInfo("No fences found, creating first fence");
                            fenceService.CreateFence("First fence");
                        }

                        loggingService.LogInfo("Universe started successfully");

                        // Run the application with HiddenMainForm
                        var smartSorterService = DependencyInjection.GetRequiredService<ISmartSorterService>();
                        Application.Run(new NoFences.UI.HiddenMainForm(trayIconManager, loggingService, smartSorterService));
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
                else
                {
                    MessageBox.Show(
                        "Universe is already running.",
                        "Universe",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
        }
    }
}
