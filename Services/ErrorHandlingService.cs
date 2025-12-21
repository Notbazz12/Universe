using System;
using System.Windows.Forms;

namespace NoFences.Services
{
    /// <summary>
    /// Global error handling service
    /// </summary>
    public interface IErrorHandlingService
    {
        void Initialize();
        void HandleException(Exception exception, string context = null);
        void ShowErrorDialog(string message, Exception exception = null);
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILoggingService _loggingService;

        public ErrorHandlingService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public void Initialize()
        {
            // Hook into unhandled exception events
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            _loggingService.LogInfo("Error handling service initialized");
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception, "Application Thread Exception");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            HandleException(exception, "Unhandled Domain Exception");

            if (e.IsTerminating)
            {
                _loggingService.LogFatal("Application is terminating due to unhandled exception", exception);
            }
        }

        public void HandleException(Exception exception, string context = null)
        {
            if (exception == null)
                return;

            var message = !string.IsNullOrEmpty(context)
                ? $"{context}: {exception.Message}"
                : exception.Message;

            _loggingService.LogError(message, exception);

            // Optionally show UI for critical errors
            if (exception is OutOfMemoryException || exception is StackOverflowException)
            {
                ShowErrorDialog("A critical error occurred. The application needs to close.", exception);
                Application.Exit();
            }
        }

        public void ShowErrorDialog(string message, Exception exception = null)
        {
            var fullMessage = message;
            if (exception != null)
            {
                fullMessage += $"\n\nDetails:\n{exception.Message}";
            }

            MessageBox.Show(
                fullMessage,
                "Error - NoFences",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
