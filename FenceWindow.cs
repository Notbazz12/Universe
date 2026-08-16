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
        private Font countBadgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);

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

        private bool isHovered;
        private bool isDragOverActive;
        private TextBox searchBox;
        private bool isSearchActive;
        private readonly List<FenceEntry> filteredEntries = new List<FenceEntry>();

        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromMilliseconds(500));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromMilliseconds(300));

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        
        // Innovative Features
        private BreathingEffect breathingEffect;
        private readonly IHistoryService historyService;
        private readonly ContextManager contextManager;
        private Timer contextTimer; // stored so it can be stopped and disposed on close

        private string contextMenuItemTarget; // Capture target for context menu actions
        
        // Cache for performance
        private Color? cachedMagicColor = null;

        // Cached GDI+ objects — created once, reused every paint, disposed on close.
        private SolidBrush _hoverBrush = new SolidBrush(Color.FromArgb(100, SystemColors.GradientActiveCaption));
        private SolidBrush _selectedBrush = new SolidBrush(Color.FromArgb(80, SystemColors.ActiveCaption));
        private Pen _outlinePen = new Pen(Color.FromArgb(120, SystemColors.ActiveBorder));

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
            this.fenceInfo = fenceInfo ?? throw new ArgumentNullException(nameof(fenceInfo));

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

            // FIX: Load the icon from the running executable itself (universe.ico is embedded
            // via <ApplicationIcon> in the .csproj). This is reliable regardless of CWD.
            // The old approach used a relative "NewLogo.png" path that broke whenever the
            // working directory was not the exe folder (e.g. launched from shortcuts or installer).
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to load icon from executable: " + ex.Message);
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
            // FIX: stored as a field so Dispose() can stop and release it
            contextTimer = new Timer { Interval = 60000 };
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

            Text = fenceInfo.Name;
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width > 50 ? fenceInfo.Width : 300;
            Height = fenceInfo.Height > 50 ? fenceInfo.Height : 300;

            prevHeight = Height;
            InitializeSearchBox();
            UpdateCachedEntries();
            Minify();
        }

        public Guid FenceId => fenceInfo.Id;

        private void InitializeSearchBox()
        {
            searchBox = new TextBox
            {
                Visible = false,
                BackColor = Color.FromArgb(20, 24, 38),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                Height = 22,
                Location = new Point(10, (titleHeight - 22) / 2),
                Width = Math.Max(120, Width - 60)
            };
            searchBox.TextChanged += (s, e) =>
            {
                UpdateFilteredEntries(searchBox.Text);
                Invalidate();
            };
            searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    ToggleSearch(false);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    if (filteredEntries.Count > 0)
                    {
                        filteredEntries[0].Open();
                        ToggleSearch(false);
                    }
                    e.Handled = true;
                }
            };
            Controls.Add(searchBox);
        }

        public void ToggleSearch(bool active)
        {
            isSearchActive = active;
            if (searchBox != null)
            {
                searchBox.Visible = active;
                searchBox.Width = Math.Max(120, Width - 60);
                if (active)
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                }
                else
                {
                    searchBox.Text = "";
                    Focus();
                }
            }
            UpdateFilteredEntries(isSearchActive ? searchBox?.Text : null);
            Invalidate();
        }

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

            // Ctrl+F - Quick Search
            if (keyData == (Keys.Control | Keys.F))
            {
                ToggleSearch(!isSearchActive);
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !fenceInfo.Locked)
            {
                e.Effect = DragDropEffects.Move;
                isDragOverActive = true;
                Invalidate();
            }
        }

        private void FenceWindow_DragLeave(object sender, EventArgs e)
        {
            isDragOverActive = false;
            Invalidate();
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            isDragOverActive = false;
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
            if (fenceInfo == null) return;

            throttledResize.Run(() =>
            {
                if (fenceInfo == null || IsDisposed) return;
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Refresh();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            // Only invalidate when hover state changes, not on every pixel of movement.
            // HitTestItem returns the path under the cursor (or null), matching
            // how the Paint loop determines hoveringItem.
            var newHover = HitTestItem(e.Location);
            if (newHover != hoveringItem)
            {
                hoveringItem = newHover;
                hasHoverUpdated = true;
                Invalidate();
            }
        }

        /// <summary>
        /// Returns the file path of the entry under <paramref name="pt"/>,
        /// or null if the cursor is not over any item.
        /// Uses the same layout math as the Paint loop.
        /// </summary>
        private string HitTestItem(Point pt)
        {
            if (filteredEntries == null || filteredEntries.Count == 0) return null;

            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;
            int currentIconSize = fenceInfo.IconSize > 0 ? fenceInfo.IconSize : 32;
            int currentItemWidth  = currentIconSize + 40;
            int currentItemHeight = currentIconSize + itemPadding + (iconFont != null ? (int)iconFont.Height : 14) + 5;

            int x = itemPadding;
            int y = itemPadding;

            foreach (var entry in filteredEntries)
            {
                int itemX = x;
                int itemY = y + effectiveTitleHeight - scrollOffset;
                var rect = new Rectangle(itemX, itemY, currentItemWidth, currentItemHeight);
                if (rect.Contains(pt))
                    return entry.Path;

                x += currentItemWidth + itemPadding;
                if (x + currentItemWidth > Width)
                {
                    x = itemPadding;
                    y += currentItemHeight + itemPadding;
                }
            }
            return null;
        }

        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            isHovered = true;
            if (fenceInfo.ChameleonMode)
            {
                this.Opacity = 1.0;
            }

            if (minifyToolStripMenuItem != null && minifyToolStripMenuItem.Checked && isMinified)
            {
                isMinified = false;
                Height = prevHeight;
            }
            Invalidate();
        }

        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            isHovered = false;
            if (fenceInfo.ChameleonMode && !this.ClientRectangle.Contains(PointToClient(MousePosition)))
            {
                this.Opacity = 0.2; // Fade out
            }

            Minify();
            selectedItem = null;
            Invalidate();
        }

        private void Minify()
        {
            if (minifyToolStripMenuItem != null && minifyToolStripMenuItem.Checked && !isMinified)
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
                // Quick Action: Execute all executables
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

        private static readonly SolidBrush textShadowBrush = new SolidBrush(Color.FromArgb(180, 10, 10, 15));
        private static readonly SolidBrush scrollbarBrush = new SolidBrush(Color.FromArgb(150, Color.Black));
        private static readonly StringFormat entryStringFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        private static readonly StringFormat titleAlignLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        private static readonly StringFormat titleAlignCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        private static readonly StringFormat titleAlignRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool isCyberGlass = fenceInfo.Theme == "CyberGlass" || string.IsNullOrEmpty(fenceInfo.Theme);

            if (isCyberGlass)
            {
                UI.CyberGlassRenderer.RenderCyberGlassContainer(
                    e.Graphics,
                    ClientRectangle,
                    isHovered,
                    isDragOverActive,
                    fenceInfo.CornerRadius > 0 ? fenceInfo.CornerRadius : 10
                );
            }
            else
            {
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

                using (var bgBrush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillRectangle(bgBrush, ClientRectangle);
                }
            }

            // Title Header
            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;
            
            if (fenceInfo.ShowHeader)
            {
                if (isCyberGlass)
                {
                    // Subtle Glass Header division line
                    using (var sepPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1.0f))
                    {
                        e.Graphics.DrawLine(sepPen, 10, titleHeight - 1, Width - 10, titleHeight - 1);
                    }
                }
                else
                {
                    using (var titleBgBrush = new SolidBrush(Color.FromArgb(fenceInfo.TitleColor)))
                    {
                        e.Graphics.FillRectangle(titleBgBrush, new RectangleF(0, 0, Width, titleHeight));
                    }
                }

                if (!isSearchActive)
                {
                    var align = fenceInfo.TitleAlignment == 0 ? titleAlignLeft :
                                fenceInfo.TitleAlignment == 2 ? titleAlignRight : titleAlignCenter;
                    
                    var titleRect = new RectangleF(14, 0, Width - 28, titleHeight);

                    using (var titleTextBrush = new SolidBrush(isCyberGlass ? Color.White : Color.FromArgb(fenceInfo.TitleTextColor)))
                    {
                        e.Graphics.DrawString(Text, titleFont, titleTextBrush, titleRect, align);
                    }

                    // Iridescent Pill Item Counter Badge
                    if (isCyberGlass && fenceInfo.ShowItemCountBadge)
                    {
                        string badgeText = $"{filteredEntries.Count}";
                        int badgeW = 28 + (badgeText.Length > 2 ? 10 : 0);
                        int badgeH = 18;
                        int badgeX = Width - badgeW - 12;
                        int badgeY = (titleHeight - badgeH) / 2;
                        UI.CyberGlassRenderer.RenderPillBadge(e.Graphics, new Rectangle(badgeX, badgeY, badgeW, badgeH), badgeText, countBadgeFont, UI.CyberGlassRenderer.IridescentCyan);
                    }
                }
            }

            // Items
            int currentIconSize = fenceInfo.IconSize > 0 ? fenceInfo.IconSize : 32;
            int currentItemWidth = currentIconSize + 40;
            int currentItemHeight = currentIconSize + itemPadding + (int)iconFont.Height + 5;

            var x = itemPadding;
            var y = itemPadding;
            scrollHeight = 0;
            
            var originalClip = e.Graphics.Clip;
            e.Graphics.SetClip(new Rectangle(0, effectiveTitleHeight, Width, Height - effectiveTitleHeight));
            
            foreach (var entry in filteredEntries)
            {
                RenderEntry(e.Graphics, entry, x, y + effectiveTitleHeight - scrollOffset, currentItemWidth, currentIconSize, isCyberGlass);

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

            e.Graphics.Clip = originalClip;

            scrollHeight -= (ClientRectangle.Height - effectiveTitleHeight);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Height - effectiveTitleHeight;
                var scrollbarHeight = contentHeight - scrollHeight;
                using (var sbBrush = isCyberGlass ? new SolidBrush(Color.FromArgb(90, UI.CyberGlassRenderer.IridescentViolet)) : (SolidBrush)scrollbarBrush)
                {
                    e.Graphics.FillRectangle(sbBrush, new Rectangle(Width - 4, effectiveTitleHeight + scrollOffset, 4, Math.Max(12, scrollbarHeight)));
                }

                scrollOffset = Math.Min(scrollOffset, scrollHeight);
            }

            if (shouldUpdateSelection && !hasSelectionUpdated)
                selectedItem = null;

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldRunDoubleClick = false;
            shouldUpdateSelection = false;
            hasSelectionUpdated = false;
            hasHoverUpdated = false;
        }

        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y, int width, int iconSize, bool isCyberGlass)
        {
            var icon = entry.ExtractIcon(thumbnailProvider);
            var name = entry.Name;

            var textPosition = new PointF(x, y + iconSize + 5);
            var textMaxSize = new SizeF(width, iconFont.Height * 2);

            var textSize = g.MeasureString(name, iconFont, textMaxSize, entryStringFormat);
            var outlineRect = new Rectangle(x - 2, y - 2, width + 2, iconSize + (int)textSize.Height + 5 + 2);

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

            bool isSelected = selectedItem == entry.Path;

            if (isCyberGlass)
            {
                // Render Floating Glass Bubble
                UI.CyberGlassRenderer.RenderBubbleCard(g, outlineRect, mouseOver, isSelected, 8);
            }
            else
            {
                if (isSelected || mouseOver)
                {
                    g.DrawRectangle(_outlinePen, outlineRect.Shrink(1));
                    g.FillRectangle(isSelected ? _selectedBrush : _hoverBrush, outlineRect);
                }
            }

            // Draw Icon (Scaled)
            if (icon != null)
            {
                g.DrawIcon(icon, new Rectangle(x + width / 2 - iconSize / 2, y, iconSize, iconSize));
            }
            
            // Draw Text
            g.DrawString(name, iconFont, textShadowBrush, new RectangleF(textPosition.Move(shadowDist, shadowDist), textMaxSize), entryStringFormat);
            g.DrawString(name, iconFont, Brushes.White, new RectangleF(textPosition, textMaxSize), entryStringFormat);
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

                                // Record individual file additions in history for accurate undo
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
            UpdateFilteredEntries(searchBox != null && isSearchActive ? searchBox.Text : null);
        }

        private void UpdateFilteredEntries(string filter)
        {
            filteredEntries.Clear();
            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredEntries.AddRange(cachedEntries);
            }
            else
            {
                foreach (var entry in cachedEntries)
                {
                    if (entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filteredEntries.Add(entry);
                    }
                }
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
            if (fenceInfo == null) return;

            throttledMove.Run(() =>
            {
                if (fenceInfo == null || IsDisposed) return;
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
            appContextMenu.Items.Add(viewItem);

            // Sort
            var sortItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("SortBy"));
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

            // Remove Fence
            appContextMenu.Items.Add(new ToolStripMenuItem(Services.LocalizationManager.GetString("RemoveFence"), null, (s, args) => 
            {
                if (MessageBox.Show(this, "Are you sure you want to remove this fence?", "Remove Fence", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    var fenceService = NoFences.Core.DependencyInjection.GetService<NoFences.Model.IFenceService>();
                    if (fenceService != null)
                        fenceService.RemoveFence(fenceInfo);
                    else
                        Close();
                }
            }));

            appContextMenu.Items.Add(new ToolStripSeparator());

            // Exit Application
            var exitItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("ExitUniverse"), null, (s, args) =>
            {
                if (MessageBox.Show(this, Services.LocalizationManager.GetString("ConfirmExit"), "Universe", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var tray = NoFences.Core.DependencyInjection.GetService<ITrayIconManager>();
                    tray?.Dispose();
                    var fenceService = NoFences.Core.DependencyInjection.GetService<NoFences.Model.IFenceService>();
                    fenceService?.CloseAllFences();
                    Application.Exit();
                    Environment.Exit(0);
                }
            });
            appContextMenu.Items.Add(exitItem);
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
            settings.BringToFront();
            settings.Activate();
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
                try
                {
                    if (Directory.Exists(hoveringItem))
                    {
                        shellContextMenu.ShowContextMenu(new[] { new DirectoryInfo(hoveringItem) }, MousePosition);
                    }
                    else if (File.Exists(hoveringItem))
                    {
                        shellContextMenu.ShowContextMenu(new[] { new FileInfo(hoveringItem) }, MousePosition);
                    }
                    else
                    {
                        appContextMenu.Show(this, e.Location);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Shell context menu error: {ex.Message}");
                    appContextMenu.Show(this, e.Location);
                }
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
            if (IsDisposed || Disposing) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(Invalidate)); } catch { }
            }
            else
            {
                Invalidate();
            }
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

            using (var g = CreateGraphics())
            {
                foreach (var entry in cachedEntries)
                {
                    var textMaxSize = new SizeF(currentItemWidth, iconFont.Height * 2);
                    var textSize = g.MeasureString(entry.Name, iconFont, textMaxSize, entryStringFormat);
                    
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
            }

            return Rectangle.Empty;
        }
    }

}

