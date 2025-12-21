using System;
using System.Drawing;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.Util;
using NoFences.Services;

namespace NoFences.UI.Pages
{
    public class PersonalizationPage : UserControl
    {
        private FenceInfo fenceInfo;
        private Action onChanged;
        
        private TrackBar hueTrack, satTrack, briTrack, alphaTrack;
        private bool isUpdating;

        public PersonalizationPage(FenceInfo fence, Action onChanged)
        {
            this.fenceInfo = fence;
            this.onChanged = onChanged;
            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;
            
            var title = new Label { Text = "Personalization", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) };
            var subtitle = new Label { Text = "Customize your Fence groups' background color & style.", Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(2, 40), ForeColor = Color.Gray };

            Controls.Add(title);
            Controls.Add(subtitle);

            // Theme Selector
            var themeGroup = new GroupBox { Text = "Quick Themes", Location = new Point(0, 70), Size = new Size(500, 80), Font = new Font("Segoe UI", 10) };
            
            themeGroup.Controls.Add(new Label { Text = "Preset:", Location = new Point(20, 30), AutoSize = true });
            var comboTheme = new ComboBox { Location = new Point(80, 25), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            comboTheme.Items.AddRange(new object[] { "Custom", "Light", "Dark", "Glass", "Minimal" });
            comboTheme.SelectedIndex = 0; // Default to "Custom"
            comboTheme.SelectedIndexChanged += (s, e) => 
            {
                if (comboTheme.SelectedIndex == 0) return; // Custom selected
                
                ThemeInfo theme = null;
                switch (comboTheme.SelectedIndex)
                {
                    case 1: theme = ThemeInfo.Light; break;
                    case 2: theme = ThemeInfo.Dark; break;
                    case 3: theme = ThemeInfo.Glass; break;
                    case 4: theme = ThemeInfo.Minimal; break;
                }
                
                if (theme != null)
                {
                    theme.ApplyTo(fenceInfo);
                    LoadValues(); // Reload the color sliders
                    onChanged?.Invoke();
                }
            };
            themeGroup.Controls.Add(comboTheme);
            
            Controls.Add(themeGroup);

            var group = new GroupBox { Text = "Background Color", Location = new Point(0, 160), Size = new Size(500, 300), Font = new Font("Segoe UI", 10) };
            
            int y = 40;
            
            // Hue
            group.Controls.Add(new Label { Text = "Hue:", Location = new Point(20, y), AutoSize = true });
            hueTrack = CreateTrackBar(0, 360, 100, y);
            group.Controls.Add(hueTrack);
            y += 50;

            // Saturation
            group.Controls.Add(new Label { Text = "Saturation:", Location = new Point(20, y), AutoSize = true });
            satTrack = CreateTrackBar(0, 100, 100, y);
            group.Controls.Add(satTrack);
            y += 50;

            // Brightness
            group.Controls.Add(new Label { Text = "Brightness:", Location = new Point(20, y), AutoSize = true });
            briTrack = CreateTrackBar(0, 100, 100, y);
            group.Controls.Add(briTrack);
            y += 50;

            // Transparency
            group.Controls.Add(new Label { Text = LocalizationManager.GetString("Transparency"), Location = new Point(20, y), AutoSize = true });
            alphaTrack = CreateTrackBar(0, 255, 100, y);
            group.Controls.Add(alphaTrack);
            y += 60;

            // Chameleon Mode
            var chkChameleon = new CheckBox { Text = LocalizationManager.GetString("ChameleonMode"), Location = new Point(20, y), AutoSize = true, Checked = fenceInfo.ChameleonMode };
            chkChameleon.CheckedChanged += (s, e) => { fenceInfo.ChameleonMode = chkChameleon.Checked; onChanged?.Invoke(); };
            group.Controls.Add(chkChameleon);
            y += 40;

            Controls.Add(group);

            // Header Options Group
            var headerGroup = new GroupBox { Text = LocalizationManager.GetString("CustomizeHeaders"), Location = new Point(0, 470), Size = new Size(500, 100), Font = new Font("Segoe UI", 10) };
            
            var chkShowHeader = new CheckBox { Text = LocalizationManager.GetString("ShowHeader"), Location = new Point(20, 30), AutoSize = true, Checked = fenceInfo.ShowHeader };
            chkShowHeader.CheckedChanged += (s, e) => { fenceInfo.ShowHeader = chkShowHeader.Checked; onChanged?.Invoke(); };
            headerGroup.Controls.Add(chkShowHeader);

            headerGroup.Controls.Add(new Label { Text = LocalizationManager.GetString("Alignment"), Location = new Point(200, 30), AutoSize = true });
            var comboAlign = new ComboBox { Location = new Point(280, 25), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            comboAlign.Items.AddRange(new object[] { "Left", "Center", "Right" });
            comboAlign.SelectedIndex = fenceInfo.TitleAlignment;
            comboAlign.SelectedIndexChanged += (s, e) => { fenceInfo.TitleAlignment = comboAlign.SelectedIndex; onChanged?.Invoke(); };
            headerGroup.Controls.Add(comboAlign);

            var btnTitleFont = new Button { Text = LocalizationManager.GetString("ChangeTitleFont"), Location = new Point(20, 60), Size = new Size(150, 30), FlatStyle = FlatStyle.System };
            btnTitleFont.Click += (s, e) => ChangeFont(true);
            headerGroup.Controls.Add(btnTitleFont);

            Controls.Add(headerGroup);

            // Icon & Item Options
            var itemGroup = new GroupBox { Text = LocalizationManager.GetString("IconsAndText"), Location = new Point(0, 580), Size = new Size(500, 100), Font = new Font("Segoe UI", 10) };

            itemGroup.Controls.Add(new Label { Text = LocalizationManager.GetString("IconSize"), Location = new Point(20, 30), AutoSize = true });
            var comboIcon = new ComboBox { Location = new Point(100, 25), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            comboIcon.Items.AddRange(new object[] { "Small (32)", "Medium (48)", "Large (64)" });
            
            // Map int to index
            if (fenceInfo.IconSize == 32) comboIcon.SelectedIndex = 0;
            else if (fenceInfo.IconSize == 48) comboIcon.SelectedIndex = 1;
            else if (fenceInfo.IconSize == 64) comboIcon.SelectedIndex = 2;
            else comboIcon.SelectedIndex = 0;

            comboIcon.SelectedIndexChanged += (s, e) => 
            {
                if (comboIcon.SelectedIndex == 0) fenceInfo.IconSize = 32;
                else if (comboIcon.SelectedIndex == 1) fenceInfo.IconSize = 48;
                else if (comboIcon.SelectedIndex == 2) fenceInfo.IconSize = 64;
                onChanged?.Invoke();
            };
            itemGroup.Controls.Add(comboIcon);

            var btnItemFont = new Button { Text = LocalizationManager.GetString("ChangeItemFont"), Location = new Point(220, 25), Size = new Size(150, 30), FlatStyle = FlatStyle.System };
            btnItemFont.Click += (s, e) => ChangeFont(false);
            itemGroup.Controls.Add(btnItemFont);

            Controls.Add(itemGroup);

            // Innovative Features Group
            var innovGroup = new GroupBox { Text = "Magic & Behavior", Location = new Point(0, 690), Size = new Size(500, 150), Font = new Font("Segoe UI", 10) };
            
            int iy = 30;
            
            // Magic Color
            var chkMagic = new CheckBox { Text = "Magic Color (Auto-detect from files)", Location = new Point(20, iy), AutoSize = true, Checked = fenceInfo.EnableMagicColor };
            chkMagic.CheckedChanged += (s, e) => { fenceInfo.EnableMagicColor = chkMagic.Checked; onChanged?.Invoke(); };
            innovGroup.Controls.Add(chkMagic);
            iy += 30;

            // Breathing Effect
            var chkBreath = new CheckBox { Text = "Breathing Effect (On new files)", Location = new Point(20, iy), AutoSize = true, Checked = fenceInfo.EnableBreathingEffect };
            chkBreath.CheckedChanged += (s, e) => { fenceInfo.EnableBreathingEffect = chkBreath.Checked; onChanged?.Invoke(); };
            innovGroup.Controls.Add(chkBreath);
            iy += 30;

            // Context
            innovGroup.Controls.Add(new Label { Text = "Context:", Location = new Point(20, iy + 3), AutoSize = true });
            var comboContext = new ComboBox { Location = new Point(100, iy), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            comboContext.Items.AddRange(new object[] { 
                "Always Visible", 
                "Weekdays (Mon-Fri)", 
                "Weekends (Sat-Sun)", 
                "Work Hours (9-5)", 
                "After Hours (5-9)", 
                "Battery Only" 
            });
            comboContext.SelectedIndex = (int)fenceInfo.Context;
            comboContext.SelectedIndexChanged += (s, e) => { fenceInfo.Context = (FenceContext)comboContext.SelectedIndex; onChanged?.Invoke(); };
            innovGroup.Controls.Add(comboContext);

            Controls.Add(innovGroup);
        }

        private void ChangeFont(bool isTitle)
        {
            using (var fd = new FontDialog())
            {
                fd.Font = isTitle ? 
                    new Font(fenceInfo.TitleFontName, fenceInfo.TitleFontSize) : 
                    new Font(fenceInfo.ItemFontName, fenceInfo.ItemFontSize);

                if (fd.ShowDialog(this) == DialogResult.OK)
                {
                    if (isTitle)
                    {
                        fenceInfo.TitleFontName = fd.Font.Name;
                        fenceInfo.TitleFontSize = fd.Font.Size;
                    }
                    else
                    {
                        fenceInfo.ItemFontName = fd.Font.Name;
                        fenceInfo.ItemFontSize = fd.Font.Size;
                    }
                    onChanged?.Invoke();
                }
            }
        }

        private TrackBar CreateTrackBar(int min, int max, int x, int y)
        {
            var tb = new TrackBar { Location = new Point(x, y), Size = new Size(350, 45), Minimum = min, Maximum = max, TickStyle = TickStyle.None };
            tb.ValueChanged += (s, e) => UpdateColor();
            return tb;
        }

        private void LoadValues()
        {
            isUpdating = true;
            var c = Color.FromArgb(fenceInfo.BackgroundColor);
            var hsl = ColorUtil.FromColor(c);

            hueTrack.Value = (int)hsl.H;
            satTrack.Value = (int)(hsl.S * 100);
            briTrack.Value = (int)(hsl.L * 100);
            alphaTrack.Value = c.A;
            isUpdating = false;
        }

        private void UpdateColor()
        {
            if (isUpdating) return;

            var hsl = new ColorUtil.HSL
            {
                H = hueTrack.Value,
                S = satTrack.Value / 100f,
                L = briTrack.Value / 100f
            };

            var c = ColorUtil.ToColor(hsl, alphaTrack.Value);
            fenceInfo.BackgroundColor = c.ToArgb();
            
            // Auto-adjust text colors based on brightness
            if (hsl.L > 0.7)
            {
                fenceInfo.TitleColor = Color.FromArgb(50, 0, 0, 0).ToArgb();
                fenceInfo.TitleTextColor = Color.Black.ToArgb();
            }
            else
            {
                fenceInfo.TitleColor = Color.FromArgb(50, 255, 255, 255).ToArgb();
                fenceInfo.TitleTextColor = Color.White.ToArgb();
            }

            onChanged?.Invoke();
        }
    }
}
