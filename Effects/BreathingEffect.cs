using System;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences.Effects
{
    /// <summary>
    /// Manages breathing animation effect for fences with new files
    /// </summary>
    public class BreathingEffect : IDisposable
    {
        private readonly Form _targetForm;
        private readonly Timer _breathingTimer;
        private float _breathingPhase = 0;
        private int _breathingCycles = 0;
        private const int MaxCycles = 15; // 30 seconds at 2 seconds per cycle
        private bool _isBreathing = false;

        public bool IsBreathing => _isBreathing;

        public BreathingEffect(Form targetForm)
        {
            _targetForm = targetForm ?? throw new ArgumentNullException(nameof(targetForm));
            
            _breathingTimer = new Timer
            {
                Interval = 100 // Update every 100ms for smooth animation
            };
            _breathingTimer.Tick += BreathingTimer_Tick;
        }

        public void StartBreathing()
        {
            if (_isBreathing) return;

            _isBreathing = true;
            _breathingPhase = 0;
            _breathingCycles = 0;
            _breathingTimer.Start();
        }

        public void StopBreathing()
        {
            _isBreathing = false;
            _breathingTimer.Stop();
            _targetForm.Opacity = 1.0; // Reset to full opacity
        }

        private void BreathingTimer_Tick(object sender, EventArgs e)
        {
            if (!_isBreathing) return;

            // Calculate breathing phase (smooth sine wave)
            _breathingPhase += 0.05f;
            
            // Opacity varies between 0.85 and 1.0
            double opacity = 0.925 + (Math.Sin(_breathingPhase) * 0.075);
            _targetForm.Opacity = Math.Max(0.85, Math.Min(1.0, opacity));

            // Check if we completed a cycle (2 PI radians)
            if (_breathingPhase >= Math.PI * 2)
            {
                _breathingPhase = 0;
                _breathingCycles++;

                // Stop after max cycles
                if (_breathingCycles >= MaxCycles)
                {
                    StopBreathing();
                }
            }
        }

        public void Dispose()
        {
            _breathingTimer?.Stop();
            _breathingTimer?.Dispose();
        }
    }
}
