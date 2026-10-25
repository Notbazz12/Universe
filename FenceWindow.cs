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
        private bool isMinified;
        private int prevHeight;

        private int scrollHeight;
        private int scrollOffset;

        private bool isHovered;
        private bool isDragOverActive;
        private TextBox searchBox;
        private Font searchFont;
        private bool isSearchActive;
        private bool isMascotLoveActive;
        private System.Windows.Forms.Timer mascotLoveTimer;
        private readonly List<FenceEntry> filteredEntries = new List<FenceEntry>();

        // Trailing-edge debounce: persist once after a drag/resize ends instead of every few hundred ms
        private readonly Debouncer debounceMove = new Debouncer(400);
        private readonly Debouncer debounceResize = new Debouncer(400);

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        // Layout cache: item rectangles in client coordinates (no scroll, no header offset).
        // Shared by painting, hit-testing and hover so the math lives in one place.
        private Rectangle[] _itemRects = new Rectangle[0];
        private bool _layoutDirty = true;
        private int _layoutKey;
        private int _contentBottom;
        private int _hoverIndex = -1;
        private int _fontVersion;
        private int _iconInvalidatePending;
        private readonly UI.CyberGlassRenderer.ContainerCache _glassCache = new UI.CyberGlassRenderer.ContainerCache();
        
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
            var oldTitle = titleFont;
            var oldItem = iconFont;
            try {
                titleFont = new Font(fenceInfo.TitleFontName, fenceInfo.TitleFontSize);
                iconFont = new Font(fenceInfo.ItemFontName, fenceInfo.ItemFontSize);
            } catch {
                // Fallback
                titleFont = new Font("Segoe UI", 12);
                iconFont = new Font("Segoe UI", 9);
            }
            oldTitle?.Dispose();
            oldItem?.Dispose();
            _fontVersion++;
            _layoutDirty = true;
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            this.fenceInfo = fenceInfo ?? throw new ArgumentNullException(nameof(fenceInfo));

            InitializeComponent();
            
            // Enable double buffering for smooth rendering
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.UserPaint |
                          ControlStyles.ResizeRedraw, true);
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
            IconCache.Shared.Loaded += IconCache_Loaded;

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
                Font = searchFont = new Font("Segoe UI", 9f),
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
            UpdateCachedEntries(true);
            Invalidate();
        }

        /// <summary>Runs any debounced move/resize save right now (call before the app exits).</summary>
        public void FlushPendingSave()
        {
            if (IsDisposed) return;
            debounceMove.Flush();
            debounceResize.Flush();
        }

        /// <summary>
        /// Adds a file on the UI thread (called by background services such as the smart sorter,
        /// which must not mutate the file list while the UI enumerates it).
        /// </summary>
        public void TryAddFile(string filePath)
        {
            if (IsDisposed || string.IsNullOrEmpty(filePath)) return;
            if (!FenceFilterHelper.MatchesFilter(filePath, fenceInfo.FilterType)) return;
            if (fenceInfo.Files.Contains(filePath)) return;

            fenceInfo.Files.Add(filePath);
            cachedMagicColor = null;
            if (fenceInfo.EnableBreathingEffect && breathingEffect != null)
                breathingEffect.StartBreathing();
            SaveAndRebuild();
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
                    UpdateHover(pt);                       // pointer left the item area (caption does not raise MouseMove)
                    if (!isHovered)
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
                    Invalidate();
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
                Invalidate();
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
                    Invalidate();
                }
                return true;
            }
            
            // Redo - Ctrl+Y
            if (keyData == (Keys.Control | Keys.Y))
            {
                if (historyService != null && historyService.CanRedo)
                {
                    historyService.Redo();
                    Invalidate();
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
            Invalidate();
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
                SaveAndRebuild();
            }
        }

        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !fenceInfo.Locked)
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool hasAllowed = false;
                if (dropped != null)
                {
                    foreach (var f in dropped)
                    {
                        if (Directory.Exists(f) || FenceFilterHelper.MatchesFilter(f, fenceInfo.FilterType))
                        {
                            hasAllowed = true;
                            break;
                        }
                    }
                }

                if (hasAllowed)
                {
                    e.Effect = DragDropEffects.Move;
                    isDragOverActive = true;
                    Invalidate();
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
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
                if (Directory.Exists(file))
                {
                    var gameDetector = Core.DependencyInjection.GetRequiredService<Services.IGameDetectionService>();
                    if (gameDetector != null && gameDetector.IsGameDirectory(file, out var gamesInFolder) && gamesInFolder.Count > 0)
                    {
                        foreach (var g in gamesInFolder)
                        {
                            if (!fenceInfo.Files.Contains(g) && ItemExists(g) && FenceFilterHelper.MatchesFilter(g, fenceInfo.FilterType))
                            {
                                fenceInfo.Files.Add(g);
                                cachedMagicColor = null;
                                added = true;
                                if (historyService != null)
                                {
                                    historyService.RecordAction(new FenceAction
                                    {
                                        FenceId = fenceInfo.Id.ToString(),
                                        Type = FenceAction.ActionType.FileAdded,
                                        FilePath = g
                                    });
                                }
                            }
                        }
                        continue;
                    }

                    var progDetector = Core.DependencyInjection.GetRequiredService<Services.IProgramDetectionService>();
                    if (progDetector != null && progDetector.IsProgramDirectory(file, out var progsInFolder) && progsInFolder.Count > 0)
                    {
                        foreach (var p in progsInFolder)
                        {
                            if (!fenceInfo.Files.Contains(p) && ItemExists(p) && FenceFilterHelper.MatchesFilter(p, fenceInfo.FilterType))
                            {
                                fenceInfo.Files.Add(p);
                                cachedMagicColor = null;
                                added = true;
                                if (historyService != null)
                                {
                                    historyService.RecordAction(new FenceAction
                                    {
                                        FenceId = fenceInfo.Id.ToString(),
                                        Type = FenceAction.ActionType.FileAdded,
                                        FilePath = p
                                    });
                                }
                            }
                        }
                        continue;
                    }
                }

                if (!fenceInfo.Files.Contains(file) && ItemExists(file) && FenceFilterHelper.MatchesFilter(file, fenceInfo.FilterType))
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
                SaveAndRebuild();
            }
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            if (fenceInfo == null) return;

            debounceResize.Run(() =>
            {
                if (fenceInfo == null || IsDisposed) return;
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Invalidate();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateHover(e.Location);
        }

        private Rectangle HeaderRect => new Rectangle(0, 0, Width, fenceInfo.ShowHeader ? titleHeight : 0);

        /// <summary>
        /// Updates the hovered item and repaints only the two items involved (plus the header
        /// when the mascot reacts to hover), instead of the whole window.
        /// </summary>
        private void UpdateHover(Point pt)
        {
            int idx = HitTestIndex(pt);
            if (idx == _hoverIndex) return;

            int old = _hoverIndex;
            _hoverIndex = idx;
            hoveringItem = idx >= 0 ? filteredEntries[idx].Path : null;

            InvalidateItem(old);
            InvalidateItem(idx);
            if (fenceInfo.ShowMascot && fenceInfo.ShowHeader)
                Invalidate(HeaderRect);
        }

        /// <summary>Forces the hover to be recomputed after scroll / zoom / layout changes.</summary>
        private void RecomputeHover()
        {
            _hoverIndex = -1;
            hoveringItem = null;
            UpdateHover(PointToClient(MousePosition));
        }

        private void InvalidateItem(int index)
        {
            if (index < 0 || index >= _itemRects.Length) return;
            var r = _itemRects[index];
            r.Offset(0, (fenceInfo.ShowHeader ? titleHeight : 0) - scrollOffset);
            r.Inflate(6, 6);
            Invalidate(r);
        }

        private static readonly int[] AvailableIconSizes = new[] { 16, 20, 24, 32, 40, 48, 64 };

        private int CalculateItemWidth(int iconSize)
        {
            if (iconSize <= 20) return iconSize + 28;
            if (iconSize <= 32) return iconSize + 36;
            return iconSize + 40;
        }

        private int CalculateItemHeight(int iconSize)
        {
            int fontH = iconFont != null ? (int)iconFont.Height : 14;
            return iconSize + itemPadding + fontH + 8;
        }

        private int CurrentIconSize => fenceInfo.IconSize > 0 ? fenceInfo.IconSize : 32;

        /// <summary>Recomputes item rectangles and scroll bounds only when something affecting them changed.</summary>
        private void EnsureLayout()
        {
            int icon = CurrentIconSize;
            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;

            int key;
            unchecked
            {
                key = 17;
                key = key * 31 + Width;
                key = key * 31 + Height;
                key = key * 31 + icon;
                key = key * 31 + filteredEntries.Count;
                key = key * 31 + effectiveTitleHeight;
                key = key * 31 + (iconFont != null ? iconFont.Height : 0);
            }
            if (!_layoutDirty && key == _layoutKey) return;

            int w = CalculateItemWidth(icon);
            int h = CalculateItemHeight(icon);

            if (_itemRects.Length != filteredEntries.Count)
                _itemRects = new Rectangle[filteredEntries.Count];

            int x = itemPadding;
            int y = itemPadding;
            for (int i = 0; i < _itemRects.Length; i++)
            {
                _itemRects[i] = new Rectangle(x, y, w, h);
                x += w + itemPadding;
                if (x + w > Width)
                {
                    x = itemPadding;
                    y += h + itemPadding;
                }
            }

            _contentBottom = _itemRects.Length == 0 ? 0 : _itemRects[_itemRects.Length - 1].Bottom;
            scrollHeight = _contentBottom - (ClientRectangle.Height - effectiveTitleHeight);
            scrollOffset = scrollHeight > 0 ? Math.Min(scrollOffset, scrollHeight) : 0;

            _layoutKey = key;
            _layoutDirty = false;
        }

        /// <summary>Index of the entry under <paramref name="pt"/> (client coordinates), or -1.</summary>
        private int HitTestIndex(Point pt)
        {
            if (filteredEntries.Count == 0) return -1;
            EnsureLayout();

            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;
            if (pt.Y < effectiveTitleHeight) return -1;

            int dy = effectiveTitleHeight - scrollOffset;
            for (int i = 0; i < _itemRects.Length; i++)
            {
                var r = _itemRects[i];
                r.Offset(0, dy);
                if (r.Contains(pt)) return i;
            }
            return -1;
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
            _hoverIndex = -1;
            hoveringItem = null;
            Invalidate();
        }

        private void Minify()
        {
            if (minifyToolStripMenuItem != null && minifyToolStripMenuItem.Checked && !isMinified)
            {
                isMinified = true;
                prevHeight = Height;
                Height = titleHeight;
                Invalidate();
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
            UpdateHover(PointToClient(MousePosition));
            var newSelection = _hoverIndex >= 0 ? filteredEntries[_hoverIndex].Path : null;
            if (newSelection == selectedItem) return;

            selectedItem = newSelection;
            Invalidate();
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

            UpdateHover(PointToClient(MousePosition));
            if (_hoverIndex >= 0)
                filteredEntries[_hoverIndex].Open();
        }

        private static readonly SolidBrush textShadowBrush = new SolidBrush(Color.FromArgb(180, 10, 10, 15));
        private static readonly SolidBrush scrollbarBrush = new SolidBrush(Color.FromArgb(150, Color.Black));
        private static readonly SolidBrush cyberScrollbarBrush = new SolidBrush(Color.FromArgb(90, UI.CyberGlassRenderer.IridescentViolet));
        private static readonly Pen headerSeparatorPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1.0f);
        private static readonly StringFormat entryStringFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        private static readonly StringFormat titleAlignLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        private static readonly StringFormat titleAlignCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        private static readonly StringFormat titleAlignRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        // Paint only draws: all input (hover, selection, double click) is handled in mouse events.
        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool isCyberGlass = fenceInfo.Theme == "CyberGlass" || string.IsNullOrEmpty(fenceInfo.Theme);

            if (isCyberGlass)
            {
                UI.CyberGlassRenderer.RenderCyberGlassContainer(
                    g,
                    ClientRectangle,
                    isHovered,
                    isDragOverActive,
                    fenceInfo.CornerRadius > 0 ? fenceInfo.CornerRadius : 10,
                    _glassCache
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
                    g.FillRectangle(bgBrush, ClientRectangle);
                }
            }

            // Title Header
            int effectiveTitleHeight = fenceInfo.ShowHeader ? titleHeight : 0;

            if (fenceInfo.ShowHeader)
            {
                if (isCyberGlass)
                {
                    // Subtle Glass Header division line
                    g.DrawLine(headerSeparatorPen, 10, titleHeight - 1, Width - 10, titleHeight - 1);
                }
                else
                {
                    using (var titleBgBrush = new SolidBrush(Color.FromArgb(fenceInfo.TitleColor)))
                    {
                        g.FillRectangle(titleBgBrush, new RectangleF(0, 0, Width, titleHeight));
                    }
                }

                if (!isSearchActive)
                {
                    var align = fenceInfo.TitleAlignment == 0 ? titleAlignLeft :
                                fenceInfo.TitleAlignment == 2 ? titleAlignRight : titleAlignCenter;

                    var titleRect = new RectangleF(14, 0, Width - 28, titleHeight);

                    if (isCyberGlass)
                    {
                        g.DrawString(Text, titleFont, Brushes.White, titleRect, align);
                    }
                    else
                    {
                        using (var titleTextBrush = new SolidBrush(Color.FromArgb(fenceInfo.TitleTextColor)))
                        {
                            g.DrawString(Text, titleFont, titleTextBrush, titleRect, align);
                        }
                    }

                    // Iridescent Pill Item Counter Badge
                    if (isCyberGlass && fenceInfo.ShowItemCountBadge)
                    {
                        string badgeText = filteredEntries.Count.ToString();
                        int badgeW = 28 + (badgeText.Length > 2 ? 10 : 0);
                        int badgeH = 18;
                        int badgeX = Width - badgeW - 12;
                        int badgeY = (titleHeight - badgeH) / 2;
                        UI.CyberGlassRenderer.RenderPillBadge(g, new Rectangle(badgeX, badgeY, badgeW, badgeH), badgeText, countBadgeFont, UI.CyberGlassRenderer.IridescentCyan);
                    }

                    // Cosmic Mascot Companion
                    if (fenceInfo.ShowMascot)
                    {
                        Image mascotImg = isMascotLoveActive ? Util.MascotProvider.Love :
                                          isDragOverActive ? Util.MascotProvider.Excited :
                                          hoveringItem != null ? Util.MascotProvider.Wink :
                                          isMinified ? Util.MascotProvider.Sleep :
                                          Util.MascotProvider.Happy;

                        if (mascotImg != null)
                        {
                            int mascotSize = Math.Max(26, titleHeight - 4);
                            int mascotX = (isCyberGlass && fenceInfo.ShowItemCountBadge) ? Width - 72 : Width - mascotSize - 12;
                            int mascotY = (titleHeight - mascotSize) / 2;
                            g.DrawImage(mascotImg, new Rectangle(mascotX, mascotY, mascotSize, mascotSize));
                        }
                    }
                }
            }

            // Items
            int currentIconSize = CurrentIconSize;
            int currentItemWidth = CalculateItemWidth(currentIconSize);
            EnsureLayout();

            var clipState = g.Save();
            g.SetClip(new Rectangle(0, effectiveTitleHeight, Width, Math.Max(0, Height - effectiveTitleHeight)), System.Drawing.Drawing2D.CombineMode.Intersect);

            int dy = effectiveTitleHeight - scrollOffset;
            var dirty = e.ClipRectangle;
            for (int i = 0; i < _itemRects.Length; i++)
            {
                var r = _itemRects[i];
                r.Offset(0, dy);

                // Skip items outside the invalidated area / scrolled out of view
                var bounds = r;
                bounds.Inflate(6, 6);
                if (!bounds.IntersectsWith(dirty)) continue;

                var entry = filteredEntries[i];
                RenderEntry(g, entry, r.X, r.Y, currentItemWidth, currentIconSize, isCyberGlass, i == _hoverIndex, entry.Path == selectedItem);
            }

            g.Restore(clipState);

            // Scroll bar
            if (scrollHeight > 0)
            {
                var contentHeight = Height - effectiveTitleHeight;
                var scrollbarHeight = contentHeight - scrollHeight;
                g.FillRectangle(isCyberGlass ? cyberScrollbarBrush : scrollbarBrush,
                    new Rectangle(Width - 4, effectiveTitleHeight + scrollOffset, 4, Math.Max(12, scrollbarHeight)));
            }
        }

        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y, int width, int iconSize, bool isCyberGlass, bool mouseOver, bool isSelected)
        {
            var icon = entry.ExtractIcon();
            var name = entry.Name;

            var textPosition = new PointF(x, y + iconSize + 5);
            var textMaxSize = new SizeF(width, iconFont.Height * 2);

            // The name height only changes with item width / font: measure once per entry, not per paint
            int textKey;
            unchecked { textKey = (width * 1000 + iconFont.Height) * 31 + _fontVersion; }
            if (entry.TextHeight < 0 || entry.TextHeightKey != textKey)
            {
                entry.TextHeight = (int)g.MeasureString(name, iconFont, textMaxSize, entryStringFormat).Height;
                entry.TextHeightKey = textKey;
            }
            var outlineRect = new Rectangle(x - 2, y - 2, width + 2, iconSize + entry.TextHeight + 5 + 2);

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
                Invalidate();
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
                            SaveAndRebuild();
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

        private readonly List<FenceEntry> cachedEntries = new List<FenceEntry>();

        /// <summary>
        /// Rebuilds the entry list from fenceInfo.Files, reusing existing entries (and so their
        /// loaded icons) and only touching the disk for files that are new. With
        /// <paramref name="revalidate"/> existing entries are re-checked against the file system.
        /// </summary>
        private void UpdateCachedEntries(bool revalidate = false)
        {
            var existing = new Dictionary<string, FenceEntry>(cachedEntries.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var cached in cachedEntries)
                existing[cached.Path] = cached;

            cachedEntries.Clear();
            foreach (var file in fenceInfo.Files)
            {
                if (existing.TryGetValue(file, out var entry) && !revalidate)
                {
                    cachedEntries.Add(entry);
                    continue;
                }

                // New file (or revalidation): hit the file system once
                var fresh = FenceEntry.FromPath(file);
                if (fresh != null)
                    cachedEntries.Add(existing.TryGetValue(file, out entry) ? entry : fresh);
            }
            UpdateFilteredEntries(searchBox != null && isSearchActive ? searchBox.Text : null);
        }

        private void UpdateFilteredEntries(string filter)
        {
            filteredEntries.Clear();

            IEnumerable<FenceEntry> source = cachedEntries;
            if (!string.IsNullOrEmpty(fenceInfo.FilterType) && !fenceInfo.FilterType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(entry => FenceFilterHelper.MatchesFilter(entry.Path, fenceInfo.FilterType));
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredEntries.AddRange(source);
            }
            else
            {
                foreach (var entry in source)
                {
                    if (entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filteredEntries.Add(entry);
                    }
                }
            }

            _layoutDirty = true;
            _hoverIndex = -1;
            hoveringItem = null;
        }

        /// <summary>Persists the fence (serialize + debounced background write). Cheap; safe to call often.</summary>
        private void Save()
        {
            FenceManager.Instance.UpdateFence(fenceInfo);
        }

        /// <summary>Persists and rebuilds the entry list; use when Files or FilterType changed.</summary>
        private void SaveAndRebuild()
        {
            Save();
            UpdateCachedEntries();
            Invalidate();
        }

        private void FenceWindow_LocationChanged(object sender, EventArgs e)
        {
            if (fenceInfo == null) return;

            debounceMove.Run(() =>
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
            var newFenceMenu = new ToolStripMenuItem(Services.LocalizationManager.GetString("NewFence") ?? "New Fence");

            var newGeneralFence = new ToolStripMenuItem("📄 General Fence");
            newGeneralFence.Click += (s, args) =>
            {
                var fenceService = NoFences.Core.DependencyInjection.GetRequiredService<NoFences.Model.IFenceService>();
                fenceService?.CreateFence("New Fence");
            };
            newFenceMenu.DropDownItems.Add(newGeneralFence);

            var newImageFence = new ToolStripMenuItem("🖼️ New Fence: Images (Auto-Import)");
            newImageFence.Click += (s, args) =>
            {
                var fenceService = NoFences.Core.DependencyInjection.GetRequiredService<NoFences.Model.IFenceService>();
                fenceService?.CreateImageFence();
            };
            newFenceMenu.DropDownItems.Add(newImageFence);

            var newTaskFence = new ToolStripMenuItem("📝 New Fence: Tasks & Notes (Auto-Import)");
            newTaskFence.Click += (s, args) =>
            {
                var fenceService = NoFences.Core.DependencyInjection.GetRequiredService<NoFences.Model.IFenceService>();
                fenceService?.CreateTaskFence();
            };
            newFenceMenu.DropDownItems.Add(newTaskFence);

            appContextMenu.Items.Add(newFenceMenu);

            // Import Folder (NEW)
            var importFolderItem = new ToolStripMenuItem("Import Folder Content");
            importFolderItem.Click += (s, args) => ImportFolder();
            appContextMenu.Items.Add(importFolderItem);

            appContextMenu.Items.Add(new ToolStripSeparator());

            // View / Icon Size
            var viewItem = new ToolStripMenuItem(Services.LocalizationManager.GetString("View") ?? "View");
            var iconSizeSubMenu = new ToolStripMenuItem("Icon Size");
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64 };
            string[] sizeLabels = { "16 px (Extra Small)", "20 px (Compact)", "24 px (Small)", "32 px (Normal)", "40 px (Medium)", "48 px (Large)", "64 px (Extra Large)" };
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                var sizeOption = new ToolStripMenuItem(sizeLabels[i]);
                sizeOption.Checked = (fenceInfo.IconSize == s);
                sizeOption.Click += (s2, args2) =>
                {
                    fenceInfo.IconSize = s;
                    Save();
                    RecomputeHover();
                    Invalidate();
                };
                iconSizeSubMenu.DropDownItems.Add(sizeOption);
            }
            viewItem.DropDownItems.Add(iconSizeSubMenu);
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
                ReloadFonts();
                cachedMagicColor = null;
                SaveAndRebuild();
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
                Invalidate();
                Save();
            }
        }

        private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
        {
            // Mascot click reaction
            if (e.Button == MouseButtons.Left && fenceInfo.ShowMascot && e.Y < titleHeight)
            {
                bool isCyberGlass = fenceInfo.Theme == "CyberGlass" || string.IsNullOrEmpty(fenceInfo.Theme);
                int mascotSize = Math.Max(26, titleHeight - 4);
                int mascotX = (isCyberGlass && fenceInfo.ShowItemCountBadge) ? Width - 72 : Width - mascotSize - 12;
                var mascotRect = new Rectangle(mascotX, (titleHeight - mascotSize) / 2, mascotSize, mascotSize);
                if (mascotRect.Contains(e.Location))
                {
                    TriggerMascotLove();
                    return;
                }
            }

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

        private void TriggerMascotLove()
        {
            isMascotLoveActive = true;
            Invalidate();
            if (mascotLoveTimer == null)
            {
                mascotLoveTimer = new System.Windows.Forms.Timer { Interval = 1600 };
                mascotLoveTimer.Tick += (s, e) =>
                {
                    mascotLoveTimer.Stop();
                    isMascotLoveActive = false;
                    Invalidate();
                };
            }
            mascotLoveTimer.Stop();
            mascotLoveTimer.Start();
        }

        private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys.HasFlag(Keys.Control))
            {
                int currentSize = fenceInfo.IconSize > 0 ? fenceInfo.IconSize : 32;
                int idx = -1;
                for (int i = 0; i < AvailableIconSizes.Length; i++)
                {
                    if (AvailableIconSizes[i] == currentSize)
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx < 0)
                {
                    idx = 3; // default 32
                    for (int i = 0; i < AvailableIconSizes.Length; i++)
                    {
                        if (AvailableIconSizes[i] >= currentSize) { idx = i; break; }
                    }
                }

                if (e.Delta > 0 && idx < AvailableIconSizes.Length - 1)
                {
                    fenceInfo.IconSize = AvailableIconSizes[idx + 1];
                    Save();
                    RecomputeHover();
                    Invalidate();
                }
                else if (e.Delta < 0 && idx > 0)
                {
                    fenceInfo.IconSize = AvailableIconSizes[idx - 1];
                    Save();
                    RecomputeHover();
                    Invalidate();
                }
                return;
            }

            if (scrollHeight < 1)
                return;

            scrollOffset -= Math.Sign(e.Delta) * 10;
            if (scrollOffset < 0)
                scrollOffset = 0;
            if (scrollOffset > scrollHeight)
                scrollOffset = scrollHeight;

            RecomputeHover();
            Invalidate();
        }

        // Raised on the icon loader thread; coalesce bursts into a single repaint on the UI thread
        private void IconCache_Loaded(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (System.Threading.Interlocked.Exchange(ref _iconInvalidatePending, 1) == 1) return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    _iconInvalidatePending = 0;
                    if (!IsDisposed) Invalidate();
                }));
            }
            catch (InvalidOperationException)
            {
                _iconInvalidatePending = 0; // handle not created / already destroyed
            }
        }

        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }
    }

}

