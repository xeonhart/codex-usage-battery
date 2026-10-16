using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexUsageBattery
{
    internal sealed class UsageWindow
    {
        public string Label;
        public double UsedPercent;
        public long ResetsAt;
        public int Minutes;
    }

    internal sealed class UsageSnapshot
    {
        public string BucketName;
        public List<UsageWindow> Windows = new List<UsageWindow>();
    }

    internal static class CodexClient
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static UsageSnapshot ReadLimits()
        {
            string codex = FindCodex();
            if (codex == null) throw new Exception("Codex CLI was not found. Install or open Codex, then refresh.");

            var info = new ProcessStartInfo(codex, "app-server --stdio");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            using (var process = new Process())
            {
                process.StartInfo = info;
                process.Start();
                try
                {
                    Write(process, "{\"method\":\"initialize\",\"id\":1,\"params\":{\"clientInfo\":{\"name\":\"codex_usage_battery\",\"title\":\"Codex Usage Battery\",\"version\":\"1.0.0\"}}}");
                    var init = ReadResponse(process, 1);
                    ThrowIfError(init);
                    Write(process, "{\"method\":\"initialized\",\"params\":{}}");
                    Write(process, "{\"method\":\"account/rateLimits/read\",\"id\":2}");
                    var response = ReadResponse(process, 2);
                    ThrowIfError(response);
                    var result = Dict(Get(response, "result"));
                    if (result == null) throw new Exception("Codex returned no usage data. Sign in to Codex and refresh.");
                    return Parse(result);
                }
                finally
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                }
            }
        }

        private static void Write(Process p, string line)
        {
            p.StandardInput.WriteLine(line);
            p.StandardInput.Flush();
        }

        private static Dictionary<string, object> ReadResponse(Process p, int id)
        {
            for (int i = 0; i < 30; i++)
            {
                var read = p.StandardOutput.ReadLineAsync();
                if (!read.Wait(15000)) throw new Exception("Codex did not answer within 15 seconds.");
                var line = read.Result;
                if (line == null) throw new Exception("Codex app-server closed before returning usage.");
                Dictionary<string, object> data;
                try { data = Dict(Json.DeserializeObject(line)); }
                catch { continue; }
                if (data != null && Number(Get(data, "id")) == id) return data;
            }
            throw new Exception("Codex returned too many unrelated messages.");
        }

        private static void ThrowIfError(Dictionary<string, object> response)
        {
            var error = Dict(Get(response, "error"));
            if (error != null)
            {
                var message = Convert.ToString(Get(error, "message"), CultureInfo.InvariantCulture);
                throw new Exception(String.IsNullOrEmpty(message) ? "Codex returned an error." : message);
            }
        }

        private static UsageSnapshot Parse(Dictionary<string, object> result)
        {
            var buckets = Dict(Get(result, "rateLimitsByLimitId"));
            Dictionary<string, object> bucket = null;
            string name = "Codex";
            if (buckets != null)
            {
                if (buckets.ContainsKey("codex"))
                {
                    bucket = Dict(buckets["codex"]);
                    name = "Codex";
                }
                else
                {
                    foreach (var pair in buckets)
                    {
                        bucket = Dict(pair.Value);
                        name = pair.Key;
                        break;
                    }
                }
            }
            if (bucket == null) bucket = Dict(Get(result, "rateLimits"));
            if (bucket == null) throw new Exception("No Codex usage limit is available for this account.");
            var label = Convert.ToString(Get(bucket, "limitName"), CultureInfo.InvariantCulture);
            if (!String.IsNullOrWhiteSpace(label)) name = label;

            var snapshot = new UsageSnapshot();
            snapshot.BucketName = name;
            AddWindow(snapshot, bucket, "primary");
            AddWindow(snapshot, bucket, "secondary");
            if (snapshot.Windows.Count == 0) throw new Exception("Codex did not provide a usage percentage.");
            return snapshot;
        }

        private static void AddWindow(UsageSnapshot target, Dictionary<string, object> bucket, string key)
        {
            var data = Dict(Get(bucket, key));
            if (data == null || Get(data, "usedPercent") == null) return;
            int mins = (int)Number(Get(data, "windowDurationMins"));
            var item = new UsageWindow();
            item.Label = mins >= 10080 && mins % 10080 == 0 ? (mins / 10080) + " week" :
                         mins >= 1440 && mins % 1440 == 0 ? (mins / 1440) + " day" :
                         mins >= 60 && mins % 60 == 0 ? (mins / 60) + " hour" : mins + " minute";
            if (mins != 60 && mins != 1440 && mins != 10080 && item.Label.EndsWith("1 week") == false && item.Label.EndsWith("1 day") == false && item.Label.EndsWith("1 hour") == false && item.Label.EndsWith("1 minute") == false) item.Label += "s";
            item.UsedPercent = Math.Max(0, Math.Min(100, Number(Get(data, "usedPercent"))));
            item.ResetsAt = (long)Number(Get(data, "resetsAt"));
            item.Minutes = mins;
            target.Windows.Add(item);
        }

        private static string FindCodex()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var bin = Path.Combine(local, "OpenAI", "Codex", "bin");
            try
            {
                if (Directory.Exists(bin))
                {
                    var files = Directory.GetFiles(bin, "codex.exe", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                        return files[0];
                    }
                }
            }
            catch { }
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            foreach (var path in paths)
            {
                try
                {
                    var candidate = Path.Combine(path.Trim().Trim('"'), "codex.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        private static Dictionary<string, object> Dict(object value) { return value as Dictionary<string, object>; }
        private static object Get(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) ? value : null;
        }
        private static double Number(object value)
        {
            if (value == null) return 0;
            double number;
            return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number) ? number : 0;
        }
    }

    internal sealed class BatteryForm : Form
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "CodexUsageBattery";
        private readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private ToolStripMenuItem topItem;
        private ToolStripMenuItem startupItem;
        private UsageSnapshot snapshot;
        private string status = "Connecting to Codex...";
        private bool refreshing;
        private bool stale;
        private DateTime lastRefresh;
        private readonly Font titleFont = new Font("Segoe UI", 10, FontStyle.Bold);
        private readonly Font labelFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font percentFont = new Font("Segoe UI", 17, FontStyle.Bold);
        private readonly Font smallFont = new Font("Segoe UI", 8);
        private readonly Font footerFont = new Font("Segoe UI", 8);

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, int wparam, int lparam);

        public BatteryForm()
        {
            Text = "Codex Usage Battery";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            DoubleBuffered = true;
            TopMost = true;
            BackColor = Color.FromArgb(16, 22, 32);
            ClientSize = new Size(370, 190);
            var bounds = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(bounds.Right - Width - 24, bounds.Top + 24);

            menu.Items.Add("Refresh now", null, (s, e) => RefreshNow());
            topItem = new ToolStripMenuItem("Always on top", null, (s, e) => { TopMost = !TopMost; topItem.Checked = TopMost; });
            topItem.Checked = true;
            menu.Items.Add(topItem);
            startupItem = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleStartup());
            startupItem.Checked = IsStartupEnabled();
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Close());
            ContextMenuStrip = menu;
            MouseDown += OnDrag;
            DoubleClick += (s, e) => RefreshNow();

            refreshTimer.Interval = 120000;
            refreshTimer.Tick += (s, e) => RefreshNow();
            refreshTimer.Start();
            Shown += (s, e) => RefreshNow();
        }

        private void OnDrag(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
        }

        private async void RefreshNow()
        {
            if (refreshing) return;
            refreshing = true;
            status = "Refreshing usage...";
            Invalidate();
            try
            {
                var fresh = await Task.Run(() => CodexClient.ReadLimits());
                if (IsDisposed) return;
                snapshot = fresh;
                stale = false;
                lastRefresh = DateTime.Now;
                status = "Updated " + lastRefresh.ToString("h:mm tt");
                ClientSize = new Size(370, fresh.Windows.Count > 1 ? 250 : 190);
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                stale = true;
                status = snapshot == null ? ex.Message : "Refresh failed; last updated " + lastRefresh.ToString("h:mm tt");
            }
            finally
            {
                refreshing = false;
                if (!IsDisposed) Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(25, 34, 49), Color.FromArgb(13, 19, 29), LinearGradientMode.Vertical))
                g.FillRectangle(bg, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(54, 69, 83))) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            DrawText(g, "CODEX  /  USAGE", titleFont, Brushes.White, 20, 17);
            using (var brush = new SolidBrush(stale ? Color.FromArgb(251, 100, 103) : Color.FromArgb(130, 213, 235)))
                DrawText(g, stale ? "STALE" : "REMAINING", smallFont, brush, Width - (stale ? 61 : 91), 21);
            using (var pen = new Pen(Color.FromArgb(50, 69, 85))) g.DrawLine(pen, 20, 47, Width - 20, 47);

            if (snapshot == null)
            {
                using (var brush = new SolidBrush(Color.FromArgb(205, 215, 225)))
                    DrawText(g, status, labelFont, brush, 21, 74, Width - 42);
                DrawFooter(g, "Double-click to refresh  •  Right-click for options");
                return;
            }

            int top = 59;
            foreach (var window in snapshot.Windows)
            {
                DrawWindow(g, window, top);
                top += 59;
            }
            var footer = refreshing ? "Refreshing usage..." : status;
            DrawFooter(g, footer + "  •  Double-click to refresh");
        }

        private void DrawWindow(Graphics g, UsageWindow w, int y)
        {
            double remaining = 100 - w.UsedPercent;
            Color fill = remaining > 50 ? Color.FromArgb(75, 216, 155) : remaining > 20 ? Color.FromArgb(248, 189, 76) : Color.FromArgb(251, 100, 103);
            using (var brush = new SolidBrush(Color.FromArgb(204, 215, 226)))
                DrawText(g, w.Label.ToUpperInvariant() + " LIMIT", labelFont, brush, 21, y);
            using (var brush = new SolidBrush(Color.FromArgb(161, 176, 191)))
                DrawText(g, ResetText(w.ResetsAt), smallFont, brush, 21, y + 37);
            using (var brush = new SolidBrush(fill))
                DrawText(g, remaining.ToString("0", CultureInfo.InvariantCulture) + "%", percentFont, brush, 279, y + 4);

            var body = new Rectangle(116, y + 5, 150, 27);
            using (var brush = new SolidBrush(Color.FromArgb(36, 48, 61))) FillRound(g, brush, body, 6);
            var inner = new Rectangle(body.X + 4, body.Y + 4, (int)Math.Round((body.Width - 8) * remaining / 100), body.Height - 8);
            if (inner.Width > 0)
            {
                using (var brush = new SolidBrush(fill)) FillRound(g, brush, inner, Math.Min(4, inner.Width / 2));
            }
            using (var pen = new Pen(Color.FromArgb(87, 105, 122), 1.2f)) DrawRound(g, pen, body, 6);
            using (var brush = new SolidBrush(Color.FromArgb(87, 105, 122))) g.FillRectangle(brush, 266, y + 12, 5, 12);
        }

        private string ResetText(long epoch)
        {
            if (epoch <= 0) return "Reset time unavailable";
            var local = DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime;
            return "Resets " + local.ToString("ddd, MMM d  h:mm tt");
        }

        private void DrawFooter(Graphics g, string value)
        {
            int y = Height - 33;
            using (var pen = new Pen(Color.FromArgb(50, 69, 85))) g.DrawLine(pen, 20, y - 8, Width - 20, y - 8);
            using (var brush = new SolidBrush(Color.FromArgb(132, 153, 170))) DrawText(g, value, footerFont, brush, 21, y, Width - 42);
        }

        private static void DrawText(Graphics g, string text, Font font, Brush brush, int x, int y, int maxWidth = 1000)
        {
            g.DrawString(text, font, brush, new RectangleF(x, y, maxWidth, font.Height + 6), StringFormat.GenericTypographic);
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        private static void FillRound(Graphics g, Brush b, Rectangle r, int radius)
        {
            using (var p = Round(r, Math.Max(1, radius))) g.FillPath(b, p);
        }
        private static void DrawRound(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (var p = Round(r, radius)) g.DrawPath(pen, p);
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return key != null && key.GetValue(RunName) != null;
            }
            catch { return false; }
        }
        private void ToggleStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) throw new Exception("Windows startup settings are unavailable.");
                    if (IsStartupEnabled()) key.DeleteValue(RunName, false);
                    else key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                }
                startupItem.Checked = IsStartupEnabled();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer.Dispose(); menu.Dispose(); titleFont.Dispose(); labelFont.Dispose(); percentFont.Dispose(); smallFont.Dispose(); footerFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        private static Mutex instance;

        [STAThread]
        private static void Main()
        {
            bool created;
            instance = new Mutex(true, "Local\\CodexUsageBatteryWidget", out created);
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BatteryForm());
            instance.ReleaseMutex();
            instance.Dispose();
        }
    }
}
