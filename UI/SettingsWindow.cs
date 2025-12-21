using System;
using System.Drawing;
using System.Windows.Forms;
using NoFences.Model;

namespace NoFences.UI
{
    public partial class SettingsWindow : Form
    {
        private Panel sidebar;
        private Panel contentPanel;
        private FenceInfo currentFence;
        private Action onSettingsChanged;

        public SettingsWindow(FenceInfo fence, Action onChanged)
        {
            this.currentFence = fence;
            this.onSettingsChanged = onChanged;
            
            InitializeComponent();
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            this.Text = "NoFences Settings";
            this.Size = new Size(800, 600);
            this.BackColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Sidebar
            sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10)
            };

            AddSidebarItem(Services.LocalizationManager.GetString("General"), true);
            AddSidebarItem(Services.LocalizationManager.GetString("Personalization"), false);
            AddSidebarItem(Services.LocalizationManager.GetString("About"), false);

            // Content
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            Controls.Add(contentPanel);
            Controls.Add(sidebar);

            // Load default page
            LoadGeneralPage();
        }

        private void AddSidebarItem(string text, bool isSelected)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = isSelected ? Color.FromArgb(220, 220, 220) : Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10)
            };
            btn.FlatAppearance.BorderSize = 0;
            
            if (text == Services.LocalizationManager.GetString("General")) btn.Click += (s, e) => LoadGeneralPage();
            if (text == Services.LocalizationManager.GetString("Personalization")) btn.Click += (s, e) => LoadPersonalizationPage();
            if (text == Services.LocalizationManager.GetString("About")) btn.Click += (s, e) => LoadAboutPage();

            sidebar.Controls.Add(btn);
            btn.BringToFront(); // Stack from top
        }

        private void LoadGeneralPage()
        {
            contentPanel.Controls.Clear();
            var page = new Pages.GeneralPage()
            {
                Dock = DockStyle.Fill
            };
            contentPanel.Controls.Add(page);
        }

        private void LoadPersonalizationPage()
        {
            contentPanel.Controls.Clear();
            var page = new Pages.PersonalizationPage(currentFence, onSettingsChanged)
            {
                Dock = DockStyle.Fill
            };
            contentPanel.Controls.Add(page);
        }

        private void LoadAboutPage()
        {
            contentPanel.Controls.Clear();
            var page = new Pages.AboutPage()
            {
                Dock = DockStyle.Fill
            };
            contentPanel.Controls.Add(page);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "SettingsWindow";
            this.ResumeLayout(false);
        }
    }
}
