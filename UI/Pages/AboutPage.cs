using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using NoFences.Services;

namespace NoFences.UI.Pages
{
    public class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;
            
            var title = new Label { Text = "Universe", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) };
            Controls.Add(title);

            var lblCreator = new Label { Text = LocalizationManager.GetString("CreatedBy"), Font = new Font("Segoe UI", 14, FontStyle.Regular), AutoSize = true, Location = new Point(2, 50) };
            Controls.Add(lblCreator);

            var lblDesc = new Label { Text = LocalizationManager.GetString("Description"), Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(2, 90), ForeColor = Color.Gray };
            Controls.Add(lblDesc);

            var lblVersion = new Label { Text = "Version 2.0 (Enhanced)", Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(2, 120), ForeColor = Color.Gray };
            Controls.Add(lblVersion);

            var linkGithub = new LinkLabel { Text = LocalizationManager.GetString("VisitGithub"), Location = new Point(2, 160), AutoSize = true, Font = new Font("Segoe UI", 10) };
            linkGithub.LinkClicked += (s, e) => Process.Start("https://github.com/Notbanzz/NoFences");
            Controls.Add(linkGithub);

            var btnUpdate = new Button { Text = "Check for Updates", Location = new Point(2, 200), AutoSize = true, Font = new Font("Segoe UI", 9) };
            btnUpdate.Click += (s, e) => 
            {
                var updateService = NoFences.Core.DependencyInjection.GetRequiredService<NoFences.Services.IUpdateService>();
                updateService.CheckForUpdates(false);
            };
            Controls.Add(btnUpdate);
        }
    }
}
