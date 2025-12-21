using NoFences.Model;
using System.Linq;
using System.Collections.Generic;
using NoFences.Util;
using NoFences.Win32;
using Peter;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NoFences.Effects;
using NoFences.Services;
using static NoFences.Win32.WindowUtil;

namespace NoFences
{
    public partial class FenceWindow : Form
    {
        private int logicalTitleHeight;
        private int titleHeight;
        private const int titleOffset = 3;
        private const int itemWidth = 75;
        private const int itemHeight = 32 + itemPadding + textHeight;
        private const int textHeight = 35;
        private const int itemPadding = 15;
        private const float shadowDist = 1.5f;

        private readonly FenceInfo fenceInfo;

        private Font titleFont;
        private Font iconFont;

        private string selectedItem;
        private string hoveringItem;
        private bool shouldUpdateSelection;
        private bool shouldRunDoubleClick;
        private bool hasSelectionUpdated;
        private bool hasHoverUpdated;
        private bool isMinified;
        private int prevHeight;

        private int scrollHeight;
        private int scrollOffset;

        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromMilliseconds(500));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromMilliseconds(300));

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        
        // Innovative Features
        private BreathingEffect breathingEffect;
        private readonly IHistoryService historyService;
        private readonly ContextManager contextManager;

        private string contextMenuItemTarget; // Capture target for context menu actions
        
        // Cache for performance
        private Color? cachedMagicColor = null;

        private void ReloadFonts()
        {
            try {
                titleFont = new Font(fenceInfo.TitleFontName, fenceInfo.TitleFontSize);
                iconFont = new Font(fenceInfo.ItemFontName, fenceInfo.ItemFontSize);
            } catch {
                // Fallback
                titleFont = new Font("Segoe UI", 12);
                iconFont = new Font("Segoe UI", 9);
            }
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            InitializeComponent();
            
            // Enable double buffering for smooth rendering
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.UserPaint, true);
            this.UpdateStyles();
            
            var config = AppConfig.Load();
            if (!config.LaptopMode)
            {
                DropShadow.ApplyShadows(this);
                BlurUtil.EnableBlur(Handle);
            }
            else
            {
                // In Laptop Mode, maybe set a solid background or simpler style
                this.BackColor = Color.FromArgb(30, 30, 30); // Dark fallback
                this.Opacity = 0.9;
            }
            WindowUtil.HideFromAltTab(Handle);
            DesktopUtil.GlueToDesktop(Handle);
            //DesktopUtil.PreventMinimize(Handle);
            logicalTitleHeight = (fenceInfo.TitleHeight < 16 || fenceInfo.TitleHeight > 100) ? 35 : fenceInfo.TitleHeight;
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
            
            this.MouseWheel += FenceWindow_MouseWheel;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;

            // Dynamic Icon
            try
            {
                if (File.Exists("NewLogo.png"))
                {
                    using (var bmp = new Bitmap("NewLogo.png"))
                    {
                        this.Icon = Icon.FromHandle(bmp.GetHicon());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to load custom icon: " + ex.Message);
            }

            // Initialize innovative features safely
            try {
                breathingEffect = new BreathingEffect(this);
                // Use safe service retrieval
                var services = NoFences.Core.DependencyInjection.GetServiceProvider();
                if (services != null)
                {
                    historyService = (IHistoryService)services.GetService(typeof(IHistoryService));
                    contextManager = (ContextManager)services.GetService(typeof(ContextManager));
                }
            } catch (Exception ex) {
                // Fail silently but log to console/debug
                System.Diagnostics.Debug.WriteLine("Error initializing features: " + ex.Message);
            }

            // Context Timer (Check every minute)
            var contextTimer = new Timer { Interval = 60000 };
            contextTimer.Tick += (s, e) => 
            {
                if (contextManager != null)
                {
                    bool shouldShow = contextManager.ShouldShowFence(fenceInfo);
                    if (shouldShow && !Visible) Show();
                    else if (!shouldShow && Visible) Hide();
                }
            };
            contextTimer.Start();

            ReloadFonts();

            AllowDrop = true;


            this.fenceInfo = fenceInfo;
            Text = fenceInfo.Name;
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width;
            Height = fenceInfo.Height;

            prevHeight = Height;
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
            UpdateCachedEntries();
            Minify();
        }

        public Guid FenceId => fenceInfo.Id;

        public void ReloadFiles()
        {
            UpdateCachedEntries();
            Refresh();
        }

        protected override void WndProc(ref Message m)
        {
            //Console.WriteLine(m.Msg.ToString("X4"));

            // Remove border
            if (m.Msg == 0x0083)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Mouse leave
            var myrect = new Rectangle(Location, Size);
            if (m.Msg == 0x02a2 && !myrect.IntersectsWith(new Rectangle(MousePosition, new Size(1, 1))))
            {
                Minify();
            }

            // Prevent maximize
            if ((m.Msg == WM_SYSCOMMAND) && m.WParam.ToInt32() == 0xF032)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Prevent foreground
            if (m.Msg == WM_SETFOCUS)
            {
                SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                return;
            }

            // Other messages
            base.WndProc(ref m);

            // If not locked and using the left mouse button
            if (MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
                return;

            // Then, allow dragging and resizing
            if (m.Msg == WM_NCHITTEST)
            {

                var pt = PointToClient(new Point(m.LParam.ToInt32()));

                if ((int)m.Result == HTCLIENT && pt.Y < titleHeight)     // drag the form
                {
                    m.Result = (IntPtr)HTCAPTION;
                    FenceWindow_MouseEnter(null, null);
                }

                if (pt.X < 10 && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > (Width - 10) && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < 10 && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > (Width - 10) && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < 10)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > (Width - 10))
                    m.Result = new IntPtr(HTRIGHT);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // F2 - Rename fence
            if (keyData == Keys.F2)
            {
                renameToolStripMenuItem_Click(this, EventArgs.Empty);
                return true;
            }

            // Delete - Remove selected item
            if (keyData == Keys.Delete && !string.IsNullOrEmpty(selectedItem))
            {
                if (MessageBox.Show(this, $"Remove '{Path.GetFileName(selectedItem)}' from this fence?", 
                    "Remove Item", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RemoveFileFromFence(selectedItem);
                    selectedItem = null;
                    Refresh();
                }
                return true;
            }

            // Ctrl+Shift+Delete - Delete this fence
            if (keyData == (Keys.Control | Keys.Shift | Keys.Delete))
            {
                if (MessageBox.Show(this, $"Delete the fence '{fenceInfo.Name}'?\\n\\nThis will remove the fence but keep the files on your desktop.", 
                    "Delete Fence", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    FenceManager.Instance.RemoveFence(fenceInfo);
                    Close();
                }
                return true;
            }

            // Escape - Deselect item
            if (keyData == Keys.Escape && !string.IsNullOrEmpty(selectedItem))
            {
                selectedItem = null;
                Refresh();
                return true;
            }

            // Undo - Ctrl+Z
            if (keyData == (Keys.Control | Keys.Z))
            {
                if (historyService != null && historyService.CanUndo)
                {
                    historyService.Undo();
                    Refresh();
                }
                return true;
            }
            
            // Redo - Ctrl+Y
            if (keyData == (Keys.Control | Keys.Y))
            {
                if (historyService != null && historyService.CanRedo)
                {
                    historyService.Redo();
                    Refresh();
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Really remove this fence?", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FenceManager.Instance.RemoveFence(fenceInfo);
                Close();
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Use captured target or fallback to hovering
            var target = !string.IsNullOrEmpty(contextMenuItemTarget) ? contextMenuItemTarget : hoveringItem;
            
            if (string.IsNullOrEmpty(target)) return;

            // History
            if (historyService != null)
            {
                historyService.RecordAction(new FenceAction
                {
                    FenceId = fenceInfo.Id.ToString(),
                    Type = FenceAction.ActionType.FileRemoved,
                    FilePath = target
                });
            }

            RemoveFileFromFence(target);
            hoveringItem = null;
            contextMenuItemTarget = null;
            Refresh();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            contextMenuItemTarget = hoveringItem; // Capture it now before it's lost
            deleteItemToolStripMenuItem.Visible = !string.IsNullOrEmpty(contextMenuItemTarget);
        }

        private void RemoveFileFromFence(string fileToRemove)
        {
            // Case-insensitive removal
            var item = fenceInfo.Files.FirstOrDefault(f => f.Equals(fileToRemove, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                fenceInfo.Files.Remove(item);
                cachedMagicColor = null; // Invalidate cache
                Save();
            }
        }

        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked)
                e.Effect = DragDropEffects.Move;
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            bool added = false;
            foreach (var file in dropped)
            {
                    if (!fenceInfo.Files.Contains(file) && ItemExists(file))
                    {
                        fenceInfo.Files.Add(file);
                        cachedMagicColor = null; // Invalidate cache
                        added = true;

                    // History
                    if (historyService != null)
                    {
                        historyService.RecordAction(new FenceAction
                        {
                            FenceId = fenceInfo.Id.ToString(),
                            Type = FenceAction.ActionType.FileAdded,
                            FilePath = file
                        });
                    }
                }
            }
            
            if (added)
            {
                if (fenceInfo.EnableBreathingEffect && breathingEffect != null)
                {
                    breathingEffect.StartBreathing();
                }
                Save();
                Refresh();
            }
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            throttledResize.Run(() =>
            {
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Refresh();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            // Use Invalidate() instead of Refresh() to reduce CPU usage
            // Only repaint the necessary areas
            Invalidate();
        }

        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            if (fenceInfo.ChameleonMode)
            {
                this.Opacity = 1.0;
            }

            if (minifyToolStripMenuItem.Checked && isMinified)
            {
                isMinified = false;
                Height = prevHeight;
            }
        }

        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            if (fenceInfo.ChameleonMode && !this.ClientRectangle.Contains(PointToClient(MousePosition)))
            {
                this.Opacity = 0.2; // Fade out
            }

            Minify();
            selectedItem = null;
            Refresh();
        }

        private void Minify()
        {
            if (minifyToolStripMenuItem.Checked && !isMinified)
            {
                isMinified = true;
                prevHeight = Height;
                Height = titleHeight;
                Refresh();
            }
        }

        private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isMinified)
            {
                Height = prevHeight;
                isMinified = false;
            }
            fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
            Save();

        }

        private void FenceWindow_Click(object sender, EventArgs e)
        {
            shouldUpdateSelection = true;
            Refresh();
        }

        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            if (ModifierKeys == Keys.Alt)
            {
                // Quick Action: Open location
                var path = fenceInfo.Files.Count > 0 ? Path.GetDirectoryName(fenceInfo.Files[0]) : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", path);
                    return;
                }
            }
            else if (ModifierKeys == Keys.Control)
            {
                // Quick Action: Execute all items (Use with caution!)
                // Maybe just execute executables? 
                foreach (var file in fenceInfo.Files)
                {
                     var ext = Path.GetExtension(file).ToLower();
                     if (ext == ".exe" || ext == ".bat" || ext == ".cmd")
                     {
                         try { System.Diagnostics.Process.Start(file); } catch { }
                     }
                }
                return;
            }

            shouldRunDoubleClick = true;
            Refresh();
        }

        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clip = new Region(ClientRectangle);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background
            // Background
            Color bgColor = Color.FromArgb(fenceInfo.BackgroundColor);
            if (fenceInfo.EnableMagicColor)
            {
                if (cachedMagicColor == null)
                {
                     var dominantType = FileTypeAnalyzer.AnalyzeDominantType(fenceInfo.Files);
                     cachedMagicColor = FileTypeAnalyzer.GetMagicColor(dominantType);
                }
                bgColor = cachedMagicColor.Value;
            }
            e.Graphics.FillRectangle(new SolidBrush(bgColor), ClientRectangle);

            // Title
            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;
            
            if (fenceInfo.ShowHeader)
            {
                StringFormat align = new StringFormat { Alignment = StringAlignment.Center };
                float titleX = Width / 2;
                
                if (fenceInfo.TitleAlignment == 0) // Left
                {
                    align.Alignment = StringAlignment.Near;
                    titleX = 10;
                }
                else if (fenceInfo.TitleAlignment == 2) // Right
                {
                    align.Alignment = StringAlignment.Far;
                    titleX = Width - 10;
                }

                e.Graphics.DrawString(Text, titleFont, new SolidBrush(Color.FromArgb(fenceInfo.TitleTextColor)), new PointF(titleX, titleOffset), align);
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(fenceInfo.TitleColor)), new RectangleF(0, 0, Width, titleHeight));
            }

            // Items
            int currentIconSize = fenceInfo.IconSize > 0 ? fenceInfo.IconSize : 32;
            int currentItemWidth = currentIconSize + 40; // Padding for text
            int currentItemHeight = currentIconSize + itemPadding + (int)iconFont.Height + 5;

            var x = itemPadding;
            var y = itemPadding;
            scrollHeight = 0;
            
            // Adjust clip region based on header visibility
            e.Graphics.Clip = new Region(new Rectangle(0, effectiveTitleHeight, Width, Height - effectiveTitleHeight));
            
            foreach (var entry in cachedEntries)
            {
                RenderEntry(e.Graphics, entry, x, y + effectiveTitleHeight - scrollOffset, currentItemWidth, currentIconSize);

                var itemBottom = y + currentItemHeight;
                if (itemBottom > scrollHeight)
                    scrollHeight = itemBottom;

                x += currentItemWidth + itemPadding;
                if (x + currentItemWidth > Width)
                {
                    x = itemPadding;
                    y += currentItemHeight + itemPadding;
                }
            }

            scrollHeight -= (ClientRectangle.Height - effectiveTitleHeight);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Height - effectiveTitleHeight;
                var scrollbarHeight = contentHeight - scrollHeight;
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(150, Color.Black)), new Rectangle(Width - 5, effectiveTitleHeight + scrollOffset, 5, scrollbarHeight));

                scrollOffset = Math.Min(scrollOffset, scrollHeight);
            }

            // Click handlers
            if (shouldUpdateSelection && !hasSelectionUpdated)
                selectedItem = null;

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldRunDoubleClick = false;
            shouldUpdateSelection = false;
            hasSelectionUpdated = false;
            hasHoverUpdated = false;
        }

        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y, int width, int iconSize)
        {
            var icon = entry.ExtractIcon(thumbnailProvider);
            var name = entry.Name;

            var textPosition = new PointF(x, y + iconSize + 5);
            var textMaxSize = new SizeF(width, iconFont.Height * 2); // Allow 2 lines

            var stringFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            var textSize = g.MeasureString(name, iconFont, textMaxSize, stringFormat);
            var outlineRect = new Rectangle(x - 2, y - 2, width + 2, iconSize + (int)textSize.Height + 5 + 2);
            var outlineRectInner = outlineRect.Shrink(1);

            var mousePos = PointToClient(MousePosition);
            var mouseOver = mousePos.X >= x && mousePos.Y >= y && mousePos.X < x + outlineRect.Width && mousePos.Y < y + outlineRect.Height;

            if (mouseOver)
            {
                hoveringItem = entry.Path;
                hasHoverUpdated = true;
            }

            if (mouseOver && shouldUpdateSelection)
            {
                selectedItem = entry.Path;
                shouldUpdateSelection = false;
                hasSelectionUpdated = true;
            }

            if (mouseOver && shouldRunDoubleClick)
            {
                shouldRunDoubleClick = false;
                entry.Open();
            }

            if (selectedItem == entry.Path)
            {
                if (mouseOver)
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, SystemColors.GradientActiveCaption)), outlineRect);
                }
                else
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.GradientInactiveCaption)), outlineRect);
                }
            }
            else
            {
                if (mouseOver)
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.ActiveCaption)), outlineRect);
                }
            }

            // Draw Icon (Scaled)
            g.DrawIcon(icon, new Rectangle(x + width / 2 - iconSize / 2, y, iconSize, iconSize));
            
            // Draw Text
            g.DrawString(name, iconFont, new SolidBrush(Color.FromArgb(180, 15, 15, 15)), new RectangleF(textPosition.Move(shadowDist, shadowDist), textMaxSize), stringFormat);
            g.DrawString(name, iconFont, Brushes.White, new RectangleF(textPosition, textMaxSize), stringFormat);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new EditDialog(Text);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // History
                if (historyService != null)
                {
                    historyService.RecordAction(new FenceAction
                    {
                        FenceId = fenceInfo.Id.ToString(),
                        Type = FenceAction.ActionType.FenceRenamed,
                        OldValue = Text,
                        NewValue = dialog.NewName
                    });
                }

                Text = dialog.NewName;
                fenceInfo.Name = Text;
                Refresh();
                Save();
            }
        }

        private void ImportFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder to import all its files into this fence.";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var files = Directory.GetFiles(fbd.SelectedPath);
                        int count = 0;
                        foreach (var file in files)
                        {
                            if (!fenceInfo.Files.Contains(file))
                            {
                                fenceInfo.Files.Add(file);
                                cachedMagicColor = null; // Invalidate cache
                                count++;
                            }
                        }
                        
                        // History
                        if (historyService != null && count > 0)
                        {
                            historyService.RecordAction(new FenceAction
                            {
                                FenceId = fenceInfo.Id.ToString(),
                                Type = FenceAction.ActionType.FileAdded,
                                FilePath = $"{count} files from {Path.GetFileName(fbd.SelectedPath)}"
                            });
                        }

                        if (count > 0)
                        {
                            Save();
                            Refresh();
                            MessageBox.Show(this, $"Successfully imported {count} items.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(this, "No new items found to import.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Error importing folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.CreateFence("New fence");
        }

        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        private readonly object saveLock = new object();
        private List<FenceEntry> cachedEntries = new List<FenceEntry>();

        private void UpdateCachedEntries()
        {
            cachedEntries.Clear();
            foreach (var file in fenceInfo.Files)
            {
                var entry = FenceEntry.FromPath(file);
                if (entry != null)
                    cachedEntries.Add(entry);
            }
        }

        private void Save()
        {
            lock (saveLock)
            {
                FenceManager.Instance.UpdateFence(fenceInfo);
                UpdateCachedEntries();
            }
        }

        private void FenceWindow_LocationChanged(object sender, EventArgs e)
        {
            throttledMove.Run(() =>
            {
                fenceInfo.PosX = Location.X;
                fenceInfo.PosY = Location.Y;
                Save();
            });
        }

        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
        }

        private void FenceWindow_Load(object sender, EventArgs e)
        {
            // Rebuild context menu for localization
            appContextMenu = new ContextMenuStrip();
            appContextMenu.Renderer = new NoFences.UI.FenceMenuRenderer();

            // Rename
            appContextMenu.Items.Add(new ToolStripMenuItem(Services.LocalizationManager.GetString("RenameFence"), null, (s, args) => renameToolStripMenuItem_Click(s, args)));
            appContextMenu.Items.Add(new ToolStripSeparator());

            // New Fence
            var newFenceItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("NewFence"));
            newFenceItem.Click += (s, args) => 
            {
                var fenceService = NoFences.Core.DependencyInjection.GetRequiredService<NoFences.Model.IFenceService>();
                fenceService.CreateFence("New Fence");
            };
            appContextMenu.Items.Add(newFenceItem);

            // Import Folder (NEW)
            var importFolderItem = new ToolStripMenuItem("Import Folder Content");
            importFolderItem.Click += (s, args) => ImportFolder();
            appContextMenu.Items.Add(importFolderItem);

            appContextMenu.Items.Add(new ToolStripSeparator());

            // View
            var viewItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("View"));
            // (Add view subitems if needed, or keep simple for now)
            appContextMenu.Items.Add(viewItem);

            // Sort
            var sortItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("SortBy"));
            // (Add sort subitems if needed)
            appContextMenu.Items.Add(sortItem);

            appContextMenu.Items.Add(new ToolStripSeparator());

            // Configure
            var configItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("ConfigureFences"));
            configItem.Click += (s, args) => OpenSettings();
            appContextMenu.Items.Add(configItem);

            // Lock
            lockedToolStripMenuItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("LockFences"), null, (s, args) => {
                lockedToolStripMenuItem.Checked = !lockedToolStripMenuItem.Checked;
                lockedToolStripMenuItem_Click(s, args);
            });
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            appContextMenu.Items.Add(lockedToolStripMenuItem);

            // Minify
            minifyToolStripMenuItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("Minify"), null, (s, args) => {
                minifyToolStripMenuItem.Checked = !minifyToolStripMenuItem.Checked;
                fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
                Save();
                Minify();
            });
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
            appContextMenu.Items.Add(minifyToolStripMenuItem);

            appContextMenu.Items.Add(new ToolStripSeparator());

            // Remove
            appContextMenu.Items.Add(new ToolStripMenuItem(Services.LocalizationManager.GetString("RemoveFence"), null, (s, args) => 
            {
                if (MessageBox.Show(this, "Are you sure you want to remove this fence?", "Remove Fence", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    FenceManager.Instance.RemoveFence(fenceInfo);
                    Close();
                }
            }));
        }

        private void OpenColorPicker(Color initialColor, Action<Color> applyColor)
        {
             // Deprecated
        }

        private void ChangeColor(Action<Color> applyColor)
        {
            // Deprecated in favor of OpenColorPicker
        }

        private void OpenSettings()
        {
            var settings = new NoFences.UI.SettingsWindow(fenceInfo, () => 
            {
                Save();
                Refresh();
            });
            settings.Show();
        }

        private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new HeightDialog(fenceInfo.TitleHeight);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                fenceInfo.TitleHeight = dialog.TitleHeight;
                logicalTitleHeight = dialog.TitleHeight;
                titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
                ReloadFonts();
                Minify();
                if (isMinified)
                {
                    Height = titleHeight;
                }
                Refresh();
                Save();
            }
        }

        private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
        {
            // Middle-click to minify
            if (e.Button == MouseButtons.Middle && e.Y < titleHeight)
            {
                Minify();
                return;
            }

            if (e.Button != MouseButtons.Right)
                return;

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                shellContextMenu.ShowContextMenu(new[] { new FileInfo(hoveringItem) }, MousePosition);
            }
            else
            {
                appContextMenu.Show(this, e.Location);
            }
        }

        private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollHeight < 1)
                return;

            scrollOffset -= Math.Sign(e.Delta) * 10;
            if (scrollOffset < 0)
                scrollOffset = 0;
            if (scrollOffset > scrollHeight)
                scrollOffset = scrollHeight;

            Invalidate();
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
        {
            Invalidate();
        }

        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>
        /// Gets the rectangle of the item at the specified point, or Empty if no item found
        /// </summary>
        private Rectangle GetItemRectAtPoint(Point point)
        {
            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;
            int currentIconSize = fenceInfo.IconSize > 0 ? fenceInfo.IconSize : 32;
            int currentItemWidth = currentIconSize + 40;
            int currentItemHeight = currentIconSize + itemPadding + (int)iconFont.Height + 5;

            var x = itemPadding;
            var y = itemPadding;

            foreach (var entry in cachedEntries)
            {
                var textMaxSize = new SizeF(currentItemWidth, iconFont.Height * 2);
                var textSize = Graphics.FromHwnd(Handle).MeasureString(entry.Name, iconFont, textMaxSize, 
                    new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                
                var itemRect = new Rectangle(x - 2, y + effectiveTitleHeight - scrollOffset - 2, 
                    currentItemWidth + 4, currentIconSize + (int)textSize.Height + 5 + 4);

                if (itemRect.Contains(point))
                {
                    return itemRect;
                }

                x += currentItemWidth + itemPadding;
                if (x + currentItemWidth > Width)
                {
                    x = itemPadding;
                    y += currentItemHeight + itemPadding;
                }
            }

            return Rectangle.Empty;
        }
    }

}

