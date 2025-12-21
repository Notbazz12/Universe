using System;
using System.Windows.Forms;
using NoFences.Model;

namespace NoFences.Services
{
    /// <summary>
    /// Manages contextual visibility of fences based on time, day, or system state
    /// </summary>
    public class ContextManager
    {
        private readonly ILoggingService _loggingService;

        public ContextManager(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public bool ShouldShowFence(FenceInfo fence)
        {
            if (fence == null) return false;
            if (fence.Context == FenceContext.Always) return true;

            var now = DateTime.Now;

            switch (fence.Context)
            {
                case FenceContext.Weekdays:
                    return now.DayOfWeek >= DayOfWeek.Monday && now.DayOfWeek <= DayOfWeek.Friday;

                case FenceContext.Weekends:
                    return now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;

                case FenceContext.WorkHours:
                    return now.Hour >= 9 && now.Hour < 17;

                case FenceContext.AfterHours:
                    return now.Hour < 9 || now.Hour >= 17;

                case FenceContext.BatteryOnly:
                    return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;

                default:
                    return true;
            }
        }

        public string GetContextDescription(FenceContext context)
        {
            switch (context)
            {
                case FenceContext.Always:
                    return "Always visible";
                case FenceContext.Weekdays:
                    return "Monday - Friday";
                case FenceContext.Weekends:
                    return "Saturday - Sunday";
                case FenceContext.WorkHours:
                    return "9 AM - 5 PM";
                case FenceContext.AfterHours:
                    return "5 PM - 9 AM";
                case FenceContext.BatteryOnly:
                    return "On battery only";
                default:
                    return "Unknown";
            }
        }
    }
}
