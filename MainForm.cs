using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PKSVModMerger;

internal sealed class MainForm : Form
{
    private static readonly Font BoldGroupHeader = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font NormalContent = new("Segoe UI", 9F, FontStyle.Regular);

    // Palette: pastel blue body, pastel red buttons (with subtle hover/press tints).
    private static readonly Color BgBlue = Color.FromArgb(225, 235, 250);
    private static readonly Color BtnRed = Color.FromArgb(250, 220, 220);
    private static readonly Color BtnRedHover = Color.FromArgb(245, 205, 205);
    private static readonly Color BtnRedPress = Color.FromArgb(235, 190, 190);
    private static readonly Color BtnBorder = Color.FromArgb(200, 160, 160);

    private static void StyleButton(RoundedButton b)
    {
        b.BackColor = BgBlue;
        b.FillColor = BtnRed;
        b.HoverColor = BtnRedHover;
        b.PressColor = BtnRedPress;
        b.OutlineColor = BtnBorder;
        b.Font = new Font(b.Font, FontStyle.Bold);
    }

    // Custom click control built on Panel — Button has its own paint pipeline that draws
    // the button's bounds rect on first show no matter how many ControlStyles you flip,
    // which left a square outline around our rounded paint until the user hovered.
    // Panel has no such pipeline; OnPaint is the only thing that draws.
    private sealed class RoundedButton : Panel
    {
        public int CornerRadius { get; set; } = 6;
        public Color FillColor { get; set; } = BtnRed;
        public Color HoverColor { get; set; } = BtnRedHover;
        public Color PressColor { get; set; } = BtnRedPress;
        public Color OutlineColor { get; set; } = BtnBorder;

        private bool _hovered;
        private bool _pressed;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = BgBlue;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); Focus(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Clear with form-matching BackColor so the corners outside the rounded path blend in.
            using (var bg = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(bg, ClientRectangle);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using var path = BuildPath(rect, CornerRadius);

            Color fill = _pressed ? PressColor : (_hovered ? HoverColor : FillColor);
            if (!Enabled) fill = ControlPaint.Light(fill);

            using (var brush = new SolidBrush(fill))
                e.Graphics.FillPath(brush, path);

            using (var pen = new Pen(OutlineColor, 1f))
                e.Graphics.DrawPath(pen, path);

            TextRenderer.DrawText(
                e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var textSize = TextRenderer.MeasureText(Text, Font);
            return new Size(textSize.Width + Padding.Horizontal + 16, textSize.Height + Padding.Vertical + 8);
        }

        private static GraphicsPath BuildPath(RectangleF r, int radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (radius <= 0 || r.Width <= d || r.Height <= d)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private static GraphicsPath BuildRoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (radius <= 0 || r.Width <= d || r.Height <= d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void RoundCorners(Control c, int radius = 6)
    {
        void update()
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using var path = BuildRoundedPath(new Rectangle(0, 0, c.Width, c.Height), radius);
            c.Region = new Region(path);
        }
        c.Resize += (_, _) => update();
        update();
    }

    private readonly Settings _settings = Settings.Load();

    // List items are these so the listbox shows the folder name while we keep
    // the real trpfd path for the merge.
    private sealed class ModEntry
    {
        public string FolderPath { get; }
        public string TrpfdPath { get; }
        public ModEntry(string folderPath, string trpfdPath)
        {
            FolderPath = folderPath;
            TrpfdPath = trpfdPath;
        }
        public override string ToString()
        {
            var name = Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrEmpty(name) ? FolderPath : name;
        }
    }

    private readonly ListBox _addList = new() { SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly RoundedButton _addAdd = new() { Text = "Add..." };
    private readonly RoundedButton _addRemove = new() { Text = "Remove" };
    private readonly RoundedButton _addUp = new() { Text = "Up" };
    private readonly RoundedButton _addDown = new() { Text = "Down" };
    private readonly TextBox _outBox = new() { ReadOnly = true };
    private readonly RoundedButton _outBrowse = new() { Text = "Browse..." };
    private readonly RoundedButton _mergeBtn = new() { Text = "Merge", Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
    private readonly RoundedButton _restoreBtn = new() { Text = "Restore old files", Font = new Font("Segoe UI", 10F) };
    private readonly TextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9F),
        WordWrap = true,
        BackColor = Color.FromArgb(30, 30, 30),
        ForeColor = Color.FromArgb(220, 220, 220),
        BorderStyle = BorderStyle.None,
    };

    public MainForm()
    {
        Text = "Pokémon SV Mod Merger";
        MinimumSize = new Size(880, 840);
        Size = new Size(980, 1080);
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(12);
        BackColor = BgBlue;
        TryLoadIcon();

        foreach (var b in new[] { _addAdd, _addRemove, _addUp, _addDown, _outBrowse, _mergeBtn, _restoreBtn })
            StyleButton(b);

        BuildLayout();
        WireEvents();

        // Buttons handle their own rounded paint (RoundedButton.OnPaint). The list and log
        // still use Region clipping since they're stock controls.
        RoundCorners(_addList);
        RoundCorners(_logBox);

        _outBox.Text = _settings.OutputFolder ?? "";
        foreach (var m in _settings.Mods)
            _addList.Items.Add(new ModEntry(m.FolderPath, m.TrpfdPath));
    }

    private void BuildLayout()
    {
        // Dock-based layout. Order of Add() matters:
        //   Top/Bottom docks each claim their slice in the order they're added;
        //   Fill takes whatever remains. Add Fill LAST.

        var modsHint = new Label
        {
            Text = "Welcome to Pokémon SV Mod Merger!\n\n- Start by selecting your base mod by clicking Add.\n- Use your largest mod as a base.\n- The mod at the top of the list will be used as the base; the rest get merged into it.",
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(4, 4, 4, 12),
        };

        var logGroup = new GroupBox
        {
            Text = "Log",
            Font = BoldGroupHeader,
            Dock = DockStyle.Bottom,
            Padding = new Padding(6),
            Height = 260,
        };
        _logBox.Dock = DockStyle.Fill;
        logGroup.Controls.Add(_logBox);

        var mergeRow = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            Padding = new Padding(0, 12, 0, 12),
        };
        _mergeBtn.Size = new Size(140, 40);
        _mergeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _restoreBtn.Size = new Size(240, 40);
        _restoreBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        mergeRow.Controls.Add(_mergeBtn);
        mergeRow.Controls.Add(_restoreBtn);
        mergeRow.Layout += (_, _) =>
        {
            // Right-align: Merge at the right edge, Restore left of it with 8px gap.
            _mergeBtn.Location = new Point(mergeRow.ClientSize.Width - _mergeBtn.Width, 12);
            _restoreBtn.Location = new Point(_mergeBtn.Left - _restoreBtn.Width - 8, 12);
            _mergeBtn.Invalidate();
            _restoreBtn.Invalidate();
        };

        var outputPicker = BuildLabeledPicker("Output Mods Folder:", _outBox, _outBrowse);
        outputPicker.Dock = DockStyle.Bottom;

        var outputHint = new Label
        {
            Text = "We'll create a folder in your mods folder that combines all of your mods' data.trpfd files into one data.trpfd file. You still need to have already installed your mods properly inside your mods folder before hitting Merge. A backup will be created of each merged file that you can restore by clicking Restore old files.",
            AutoSize = true,
            MaximumSize = new Size(940, 0),
            Dock = DockStyle.Bottom,
            Padding = new Padding(4, 12, 4, 4),
        };

        var modsGroup = BuildAddBlock();
        modsGroup.Dock = DockStyle.Fill;

        // Dock=Bottom rule: LATER added = OUTERMOST (closer to the bottom edge).
        // So innermost-bottom (just below modsGroup) goes first; bottommost goes last.
        Controls.Add(modsGroup);    // Fill — middle region
        Controls.Add(modsHint);     // Top
        Controls.Add(outputHint);   // Bottom (innermost — just below modsGroup)
        Controls.Add(outputPicker); // Bottom (below outputHint)
        Controls.Add(mergeRow);     // Bottom (below outputPicker)
        Controls.Add(logGroup);     // Bottom (outermost — bottom of form)
    }

    private static GroupBox BuildLabeledPicker(string label, TextBox box, RoundedButton browse)
    {
        var group = new GroupBox { Text = label, Font = BoldGroupHeader, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Font = NormalContent };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 4, 6, 4);
        browse.AutoSize = true;
        browse.Margin = new Padding(0, 2, 0, 2);
        row.Controls.Add(box, 0, 0);
        row.Controls.Add(browse, 1, 0);
        group.Controls.Add(row);
        return group;
    }

    private GroupBox BuildAddBlock()
    {
        var group = new GroupBox { Text = "Mods to merge", Font = BoldGroupHeader, Dock = DockStyle.Fill, Padding = new Padding(6) };

        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Font = NormalContent };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _addList.Dock = DockStyle.Fill;
        _addList.Margin = new Padding(0, 0, 6, 0);
        _addList.Font = new Font("Consolas", 9F);
        row.Controls.Add(_addList, 0, 0);

        var btnCol = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
        };
        foreach (var b in new[] { _addAdd, _addRemove, _addUp, _addDown })
        {
            b.AutoSize = true;
            b.Width = 90;
            b.Margin = new Padding(0, 0, 0, 4);
            btnCol.Controls.Add(b);
        }
        row.Controls.Add(btnCol, 1, 0);

        group.Controls.Add(row);
        return group;
    }

    private void WireEvents()
    {
        _addAdd.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Pick a mod folder (must contain a data.trpfd somewhere inside)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var folder = dlg.SelectedPath;
            var trpfd = FindTrpfdIn(folder);
            if (trpfd == null)
            {
                MessageBox.Show(this,
                    $"No data.trpfd found in:\n{folder}\n\nExpected something like <mod>\\romfs\\arc\\data.trpfd",
                    "Not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _addList.Items.Add(new ModEntry(folder, trpfd));
            PersistMods();
        };
        _addRemove.Click += (_, _) =>
        {
            var idx = new List<int>();
            foreach (int i in _addList.SelectedIndices) idx.Add(i);
            idx.Sort((a, b) => b.CompareTo(a));
            foreach (var i in idx) _addList.Items.RemoveAt(i);
            PersistMods();
        };
        _addUp.Click += (_, _) => { MoveSelection(-1); PersistMods(); };
        _addDown.Click += (_, _) => { MoveSelection(+1); PersistMods(); };
        _outBrowse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Pick your mods folder (the parent folder that holds each mod subfolder)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _outBox.Text = dlg.SelectedPath;
                _settings.OutputFolder = dlg.SelectedPath;
                _settings.Save();
            }
        };
        _mergeBtn.Click += async (_, _) => await DoMerge();
        _restoreBtn.Click += (_, _) => DoRestore();
    }

    private void PersistMods()
    {
        _settings.Mods.Clear();
        foreach (var item in _addList.Items)
        {
            var me = (ModEntry)item!;
            _settings.Mods.Add(new Settings.ModRef { FolderPath = me.FolderPath, TrpfdPath = me.TrpfdPath });
        }
        _settings.Save();
    }

    private void MoveSelection(int delta)
    {
        if (_addList.SelectedIndices.Count != 1) return;
        int i = _addList.SelectedIndex;
        int j = i + delta;
        if (j < 0 || j >= _addList.Items.Count) return;
        var item = _addList.Items[i];
        _addList.Items.RemoveAt(i);
        _addList.Items.Insert(j, item);
        _addList.SelectedIndex = j;
    }

    private async Task DoMerge()
    {
        var outFolder = _outBox.Text.Trim();
        var allPaths = new List<string>();
        foreach (var item in _addList.Items)
            allPaths.Add(((ModEntry)item!).TrpfdPath);

        if (allPaths.Count < 2)
        {
            MessageBox.Show(this,
                "Add at least two mod folders. The top of the list is treated as the base; the rest are merged into it.",
                "Not enough mods", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrEmpty(outFolder))
        {
            MessageBox.Show(this, "Choose an output mods folder.", "Missing output folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Directory.Exists(outFolder))
        {
            MessageBox.Show(this, $"Output folder doesn't exist:\n{outFolder}", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        foreach (var p in allPaths)
        {
            if (!File.Exists(p))
            {
                MessageBox.Show(this, $"Mod TRPFD not found:\n{p}", "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var basePath = allPaths[0];
        var addPaths = allPaths.Skip(1).ToList();
        var outPath = Path.Combine(outFolder, "AAA_Master", "romfs", "arc", "data.trpfd");

        _mergeBtn.Enabled = false;
        _logBox.Clear();
        var startedAt = DateTime.Now;

        try
        {
            var result = await Task.Run(() => Merger.Run(basePath, addPaths, outPath, AppendLog));
            var elapsed = DateTime.Now - startedAt;
            AppendLog($"[done ] {elapsed.TotalSeconds:F1}s — active={result.FinalActive} unused={result.FinalUnused}");
            BackupSourceTrpfds(outPath);
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] {ex.GetType().Name}: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Merge failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _mergeBtn.Enabled = true;
        }
    }

    // After a successful merge, rename each source mod's data.trpfd to data.trpfd.bak so
    // the emulator only sees the merged TRPFD in AAA_Master. Skip the output path itself
    // (in case the user picked an output folder where AAA_Master is also in the mod list).
    private void BackupSourceTrpfds(string outPath)
    {
        var updates = new List<(int index, string newPath)>();
        for (int i = 0; i < _addList.Items.Count; i++)
        {
            var me = (ModEntry)_addList.Items[i]!;
            if (string.Equals(me.TrpfdPath, outPath, StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"[bak  ] {me} — skipped (same path as output)");
                continue;
            }
            if (!me.TrpfdPath.EndsWith(".trpfd", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"[bak  ] {me} — already backed up");
                continue;
            }

            var bakPath = me.TrpfdPath + ".bak";
            try
            {
                if (File.Exists(bakPath))
                {
                    // Existing backup is the true original — keep it, drop the loose .trpfd.
                    File.Delete(me.TrpfdPath);
                    AppendLog($"[bak  ] {me} — removed loose data.trpfd (older backup preserved)");
                }
                else
                {
                    File.Move(me.TrpfdPath, bakPath);
                    AppendLog($"[bak  ] {me} — data.trpfd → data.trpfd.bak");
                }
                updates.Add((i, bakPath));
            }
            catch (Exception ex)
            {
                AppendLog($"[bak warn] {me}: {ex.Message}");
            }
        }

        foreach (var (i, newPath) in updates)
        {
            var old = (ModEntry)_addList.Items[i]!;
            _addList.Items[i] = new ModEntry(old.FolderPath, newPath);
        }
        if (updates.Count > 0) PersistMods();
    }

    private void DoRestore()
    {
        if (_addList.Items.Count == 0)
        {
            MessageBox.Show(this, "Add the mods you want to restore to the list first.", "Nothing to restore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _logBox.Clear();
        AppendLog("[restore] renaming data.trpfd.bak → data.trpfd for each listed mod...");

        var updates = new List<(int index, string newPath)>();
        int restored = 0, skipped = 0;
        for (int i = 0; i < _addList.Items.Count; i++)
        {
            var me = (ModEntry)_addList.Items[i]!;
            if (!me.TrpfdPath.EndsWith(".trpfd.bak", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"[skip ] {me} — no backup tracked");
                skipped++;
                continue;
            }

            var origPath = me.TrpfdPath.Substring(0, me.TrpfdPath.Length - 4);
            try
            {
                if (File.Exists(origPath))
                {
                    AppendLog($"[skip ] {me} — data.trpfd already present, leaving .bak alone");
                    skipped++;
                    continue;
                }
                File.Move(me.TrpfdPath, origPath);
                AppendLog($"[restore] {me} — data.trpfd.bak → data.trpfd");
                updates.Add((i, origPath));
                restored++;
            }
            catch (Exception ex)
            {
                AppendLog($"[restore err] {me}: {ex.Message}");
            }
        }

        foreach (var (i, newPath) in updates)
        {
            var old = (ModEntry)_addList.Items[i]!;
            _addList.Items[i] = new ModEntry(old.FolderPath, newPath);
        }
        if (updates.Count > 0) PersistMods();

        AppendLog($"[done ] restored={restored}, skipped={skipped}. Disable or delete the AAA_Master folder in your mod manager if you want to fully revert.");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Force a clean redraw once the form is fully laid out — eliminates any first-paint
        // artifacts from the initial layout pass before all controls knew their final size.
        foreach (var b in new[] { _addAdd, _addRemove, _addUp, _addDown, _outBrowse, _mergeBtn, _restoreBtn })
            b.Invalidate();
    }

    private void AppendLog(string line)
    {
        if (_logBox.InvokeRequired)
        {
            _logBox.BeginInvoke(new Action<string>(AppendLog), line);
            return;
        }
        _logBox.AppendText(line + Environment.NewLine);
    }

    private void TryLoadIcon()
    {
        // ApplicationIcon in the csproj embeds the icon into the .exe so Explorer/taskbar
        // pick it up automatically, but the form's title bar still needs Form.Icon set.
        // ExtractAssociatedIcon pulls the embedded icon out of our own exe — no separate
        // file needed at runtime even if the loose .ico isn't beside the binary.
        try
        {
            var ico = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            if (ico != null) Icon = ico;
        }
        catch
        {
            // Icon is cosmetic — don't crash if loading fails.
        }
    }

    private static string? FindTrpfdIn(string folder)
    {
        // Standard mod layout first.
        var standard = Path.Combine(folder, "romfs", "arc", "data.trpfd");
        if (File.Exists(standard)) return standard;

        // User may have already renamed (e.g. after a previous deploy).
        var standardBak = Path.Combine(folder, "romfs", "arc", "data.trpfd.bak");
        if (File.Exists(standardBak)) return standardBak;

        // Fallback: recursive search. Prefer shortest path on ties.
        var matches = Directory.EnumerateFiles(folder, "data.trpfd", SearchOption.AllDirectories)
            .OrderBy(p => p.Length).FirstOrDefault();
        if (matches != null) return matches;

        return Directory.EnumerateFiles(folder, "data.trpfd.bak", SearchOption.AllDirectories)
            .OrderBy(p => p.Length).FirstOrDefault();
    }
}
