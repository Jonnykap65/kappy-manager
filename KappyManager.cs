using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KappyManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color Back = Color.FromArgb(7, 10, 28);
        private readonly Color Surface = Color.FromArgb(14, 20, 48);
        private readonly Color SurfaceRaised = Color.FromArgb(20, 28, 62);
        private readonly Color NavBack = Color.FromArgb(5, 7, 22);
        private readonly Color Accent = Color.FromArgb(72, 128, 255);
        private readonly Color AccentBright = Color.FromArgb(112, 185, 255);
        private readonly Color TextMain = Color.FromArgb(239, 245, 255);
        private readonly Color TextMuted = Color.FromArgb(149, 164, 201);
        private readonly Color Border = Color.FromArgb(38, 52, 101);
        private readonly Color Danger = Color.FromArgb(220, 72, 98);

        private readonly Panel content = new Panel();
        private readonly Panel nav = new Panel();
        private readonly Panel mainHost = new Panel();
        private readonly Panel toolbar = new Panel();
        private readonly Label pageTitle = new Label();
        private readonly Label pageSubtitle = new Label();
        private readonly Panel searchBox = new Panel();
        private readonly TextBox search = new TextBox();
        private readonly Button runTaskButton = new Button();
        private readonly Button endTaskButton = new Button();
        private readonly Button endAllButton = new Button();
        private readonly Button efficiencyButton = new Button();
        private readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        private readonly Dictionary<string, Button> navButtons = new Dictionary<string, Button>();
        private readonly Timer refreshTimer = new Timer();
        private readonly Stopwatch sampleClock = Stopwatch.StartNew();
        private readonly Dictionary<int, TimeSpan> processCpu = new Dictionary<int, TimeSpan>();

        private DataGridView processGrid;
        private DataGridView detailGrid;
        private DataGridView startupGrid;
        private DataGridView usersGrid;
        private DataGridView serviceGrid;
        private Label processSummary;
        private Label performanceCpu;
        private Label performanceMemory;
        private Label performanceNetwork;
        private Label performanceSystem;
        private Panel cpuGraph;
        private Panel memoryGraph;
        private Label cpuGraphValue;
        private Label memoryGraphValue;
        private readonly Dictionary<DataGridView, GridSortState> gridSortStates = new Dictionary<DataGridView, GridSortState>();
        private readonly List<float> cpuHistory = new List<float>();
        private readonly List<float> memoryHistory = new List<float>();
        private long previousSystemIdle;
        private long previousSystemKernel;
        private long previousSystemUser;
        private long previousNetworkBytes;
        private DateTime previousNetworkTime = DateTime.UtcNow;
        private DateTime lastProcessSample = DateTime.UtcNow;
        private int tickCount;
        private string activePage = "Processes";
        private readonly Dictionary<string, string> pageDescriptions = new Dictionary<string, string>
        {
            { "Processes", "Monitor active apps and background processes" },
            { "Performance", "Live system utilization and resource history" },
            { "Startup apps", "Review applications configured to start with Windows" },
            { "Users", "View active sessions and their resource usage" },
            { "Details", "Inspect process IDs, priorities, and executable paths" },
            { "Services", "Review and control Windows services" }
        };

        public MainForm()
        {
            Text = "Kappy Manager";
            try
            {
                Icon executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                Icon = executableIcon != null ? (Icon)executableIcon.Clone() : SystemIcons.Application;
                if (executableIcon != null) executableIcon.Dispose();
            }
            catch { Icon = SystemIcons.Application; }
            MinimumSize = new Size(1040, 680);
            Size = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Back;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;

            BuildLayout();
            BuildPages();
            ShowPage("Processes");
            SeedSystemCpu();
            RefreshAll(true);

            refreshTimer.Interval = 1100;
            refreshTimer.Tick += delegate { RefreshAll(false); };
            refreshTimer.Start();
        }

        private void BuildLayout()
        {
            nav.Dock = DockStyle.Left;
            nav.Width = 220;
            nav.BackColor = NavBack;
            nav.Padding = new Padding(12, 10, 12, 12);
            Controls.Add(nav);

            Panel brandPanel = new Panel();
            brandPanel.Dock = DockStyle.Top;
            brandPanel.Height = 72;
            brandPanel.BackColor = NavBack;
            nav.Controls.Add(brandPanel);

            Label brandMark = new Label();
            brandMark.Text = "K";
            brandMark.Font = new Font("Segoe UI Black", 15F);
            brandMark.ForeColor = Color.White;
            brandMark.BackColor = Accent;
            brandMark.Size = new Size(38, 38);
            brandMark.Location = new Point(4, 10);
            brandMark.TextAlign = ContentAlignment.MiddleCenter;
            brandPanel.Controls.Add(brandMark);

            Label brand = new Label();
            brand.Text = "Kappy Manager";
            brand.Font = new Font("Segoe UI Semibold", 11.5F);
            brand.ForeColor = TextMain;
            brand.AutoSize = true;
            brand.Location = new Point(53, 11);
            brandPanel.Controls.Add(brand);

            Label brandTagline = new Label();
            brandTagline.Text = "SYSTEM CONTROL";
            brandTagline.Font = new Font("Segoe UI Semibold", 7F);
            brandTagline.ForeColor = AccentBright;
            brandTagline.AutoSize = true;
            brandTagline.Location = new Point(54, 34);
            brandPanel.Controls.Add(brandTagline);

            string[] items = { "Processes", "Performance", "Startup apps", "Users", "Details", "Services" };
            string[] icons = { "●", "⌁", "↗", "◎", "≡", "◆" };
            for (int i = items.Length - 1; i >= 0; i--)
            {
                string item = items[i];
                Button button = new Button();
                button.Text = "   " + icons[i] + "     " + item;
                button.Tag = item;
                button.Dock = DockStyle.Top;
                button.Height = 48;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 29, 65);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(26, 38, 82);
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.ForeColor = TextMuted;
                button.BackColor = NavBack;
                button.Cursor = Cursors.Hand;
                button.Click += delegate(object sender, EventArgs args)
                {
                    ShowPage((string)((Button)sender).Tag);
                };
                nav.Controls.Add(button);
                navButtons[item] = button;
            }

            Panel navFooter = new Panel();
            navFooter.Dock = DockStyle.Bottom;
            navFooter.Height = 82;
            navFooter.BackColor = Color.FromArgb(9, 13, 35);
            nav.Controls.Add(navFooter);
            Label statusDot = new Label();
            statusDot.Text = "●";
            statusDot.ForeColor = Color.FromArgb(67, 215, 145);
            statusDot.AutoSize = true;
            statusDot.Location = new Point(12, 13);
            navFooter.Controls.Add(statusDot);
            Label statusText = new Label();
            statusText.Text = "Live monitoring active";
            statusText.ForeColor = TextMuted;
            statusText.AutoSize = true;
            statusText.Location = new Point(31, 14);
            navFooter.Controls.Add(statusText);

            Label copyright = new Label();
            copyright.Text = "© 2026 Jonthan Kaplan";
            copyright.ForeColor = Color.FromArgb(112, 128, 166);
            copyright.Font = new Font("Segoe UI", 7.5F);
            copyright.AutoSize = true;
            copyright.Location = new Point(12, 49);
            navFooter.Controls.Add(copyright);

            mainHost.Dock = DockStyle.Fill;
            mainHost.BackColor = Back;
            Controls.Add(mainHost);

            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 104;
            toolbar.BackColor = Back;
            toolbar.Padding = new Padding(26, 14, 22, 8);
            mainHost.Controls.Add(toolbar);

            Panel accentLine = new Panel();
            accentLine.Dock = DockStyle.Top;
            accentLine.Height = 2;
            accentLine.BackColor = Accent;
            toolbar.Controls.Add(accentLine);

            pageTitle.Text = "Processes";
            pageTitle.Font = new Font("Segoe UI Semibold", 20F);
            pageTitle.AutoSize = true;
            pageTitle.Location = new Point(26, 18);
            pageTitle.ForeColor = TextMain;
            toolbar.Controls.Add(pageTitle);

            pageSubtitle.Text = pageDescriptions["Processes"];
            pageSubtitle.Font = new Font("Segoe UI", 9F);
            pageSubtitle.AutoSize = true;
            pageSubtitle.Location = new Point(28, 57);
            pageSubtitle.ForeColor = TextMuted;
            toolbar.Controls.Add(pageSubtitle);

            searchBox.BackColor = SurfaceRaised;
            searchBox.Size = new Size(276, 38);
            searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchBox.Location = new Point(toolbar.Width - 756, 27);
            searchBox.Paint += delegate(object sender, PaintEventArgs e)
            {
                Control c = (Control)sender;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, c.ClientSize.Width - 1, c.ClientSize.Height - 1);
            };
            toolbar.Controls.Add(searchBox);
            toolbar.Resize += delegate
            {
                searchBox.Left = toolbar.ClientSize.Width - 756;
            };

            Label searchIcon = new Label();
            searchIcon.Text = "⌕";
            searchIcon.Font = new Font("Segoe UI", 15F);
            searchIcon.ForeColor = TextMuted;
            searchIcon.Location = new Point(9, 5);
            searchIcon.Size = new Size(25, 25);
            searchBox.Controls.Add(searchIcon);

            search.BorderStyle = BorderStyle.None;
            search.Font = new Font("Segoe UI", 10F);
            search.ForeColor = TextMain;
            search.BackColor = SurfaceRaised;
            search.Location = new Point(37, 9);
            search.Width = 228;
            search.TextChanged += delegate
            {
                if (activePage == "Processes" || activePage == "Details")
                    RefreshProcesses();
            };
            searchBox.Controls.Add(search);

            runTaskButton.Text = "Run new task";
            runTaskButton.Size = new Size(112, 38);
            runTaskButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            runTaskButton.Location = new Point(toolbar.Width - 462, 27);
            StyleToolbarButton(runTaskButton);
            runTaskButton.Click += delegate { RunNewTask(); };
            toolbar.Controls.Add(runTaskButton);

            efficiencyButton.Text = "Efficiency";
            efficiencyButton.Size = new Size(98, 38);
            efficiencyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            efficiencyButton.Location = new Point(toolbar.Width - 340, 27);
            StyleToolbarButton(efficiencyButton);
            efficiencyButton.Click += delegate { SetEfficiencyMode(); };
            toolbar.Controls.Add(efficiencyButton);

            endAllButton.Text = "End all";
            endAllButton.Size = new Size(98, 38);
            endAllButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            endAllButton.Location = new Point(toolbar.Width - 232, 27);
            StyleToolbarButton(endAllButton);
            endAllButton.BackColor = Color.FromArgb(42, 23, 47);
            endAllButton.ForeColor = Color.FromArgb(255, 183, 202);
            endAllButton.FlatAppearance.BorderColor = Color.FromArgb(104, 50, 82);
            endAllButton.Click += delegate { EndAllMatchingProcesses(); };
            toolbar.Controls.Add(endAllButton);

            endTaskButton.Text = "End task";
            endTaskButton.Size = new Size(100, 38);
            endTaskButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            endTaskButton.Location = new Point(toolbar.Width - 122, 27);
            StyleToolbarButton(endTaskButton);
            endTaskButton.BackColor = Color.FromArgb(53, 20, 39);
            endTaskButton.ForeColor = Color.FromArgb(255, 173, 188);
            endTaskButton.FlatAppearance.BorderColor = Color.FromArgb(126, 43, 69);
            endTaskButton.Click += delegate { EndSelectedProcess(); };
            toolbar.Controls.Add(endTaskButton);

            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(26, 0, 22, 22);
            content.BackColor = Back;
            mainHost.Controls.Add(content);

            // Keep the header and workspace in one dock container. This prevents
            // the fill area from extending underneath and being masked by the header.
            mainHost.Controls.SetChildIndex(content, 0);
            mainHost.Controls.SetChildIndex(toolbar, 1);
            Controls.SetChildIndex(mainHost, 0);
            Controls.SetChildIndex(nav, 1);
        }

        private Button MakeToolbarButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(width, 38);
            StyleToolbarButton(button);
            return button;
        }

        private void StyleToolbarButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 44, 91);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(38, 54, 108);
            button.BackColor = SurfaceRaised;
            button.ForeColor = TextMain;
            button.Cursor = Cursors.Hand;
        }

        private void BuildPages()
        {
            BuildProcessesPage();
            BuildPerformancePage();
            BuildStartupPage();
            BuildUsersPage();
            BuildDetailsPage();
            BuildServicesPage();
        }

        private Panel NewPage()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = Back;
            page.Padding = new Padding(1);
            page.Visible = false;
            page.Paint += delegate(object sender, PaintEventArgs e)
            {
                Control c = (Control)sender;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, c.ClientSize.Width - 1, c.ClientSize.Height - 1);
            };
            content.Controls.Add(page);
            return page;
        }

        private void BuildProcessesPage()
        {
            Panel page = NewPage();
            processSummary = new Label();
            processSummary.Dock = DockStyle.Top;
            processSummary.Height = 48;
            processSummary.Text = "Loading processes…";
            processSummary.ForeColor = TextMuted;
            processSummary.Font = new Font("Segoe UI", 9.5F);
            processSummary.BackColor = SurfaceRaised;
            processSummary.Padding = new Padding(14, 0, 0, 0);
            processSummary.TextAlign = ContentAlignment.MiddleLeft;
            page.Controls.Add(processSummary);

            processGrid = NewGrid();
            AddTextColumn(processGrid, "Name", "Name", 270);
            AddTextColumn(processGrid, "PID", "PID", 76);
            AddTextColumn(processGrid, "Status", "Status", 90);
            AddTextColumn(processGrid, "CPU", "CPU", 90);
            AddTextColumn(processGrid, "Memory", "Memory", 110);
            AddTextColumn(processGrid, "Threads", "Threads", 80);
            AddTextColumn(processGrid, "Description", "Description", 250);
            processGrid.Dock = DockStyle.Fill;
            processGrid.CellDoubleClick += delegate { FocusSelectedProcess(); };
            page.Controls.Add(processGrid);
            page.Controls.SetChildIndex(processGrid, 0);
            page.Controls.SetChildIndex(processSummary, 1);
            pages["Processes"] = page;
        }

        private void BuildDetailsPage()
        {
            Panel page = NewPage();
            detailGrid = NewGrid();
            AddTextColumn(detailGrid, "Name", "Name", 230);
            AddTextColumn(detailGrid, "PID", "PID", 76);
            AddTextColumn(detailGrid, "Status", "Status", 85);
            AddTextColumn(detailGrid, "User", "Session", 90);
            AddTextColumn(detailGrid, "CPU", "CPU", 85);
            AddTextColumn(detailGrid, "Memory", "Memory", 105);
            AddTextColumn(detailGrid, "Threads", "Threads", 75);
            AddTextColumn(detailGrid, "Priority", "Priority", 105);
            AddTextColumn(detailGrid, "Path", "Image path", 350);
            detailGrid.Dock = DockStyle.Fill;
            page.Controls.Add(detailGrid);
            pages["Details"] = page;
        }

        private void BuildPerformancePage()
        {
            Panel page = NewPage();
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            layout.Padding = new Padding(12);
            layout.BackColor = Surface;
            page.Controls.Add(layout);

            cpuGraph = NewGraphPanel("CPU", out cpuGraphValue);
            cpuGraph.Paint += delegate(object sender, PaintEventArgs e) { DrawGraph(e.Graphics, cpuGraph, cpuHistory, Accent); };
            layout.Controls.Add(cpuGraph, 0, 0);
            layout.SetColumnSpan(cpuGraph, 2);

            memoryGraph = NewGraphPanel("Memory", out memoryGraphValue);
            memoryGraph.Paint += delegate(object sender, PaintEventArgs e) { DrawGraph(e.Graphics, memoryGraph, memoryHistory, Color.FromArgb(151, 96, 255)); };
            layout.Controls.Add(memoryGraph, 0, 1);

            Panel stats = new Panel();
            stats.Dock = DockStyle.Fill;
            stats.BackColor = SurfaceRaised;
            stats.Margin = new Padding(8, 8, 0, 0);
            stats.Padding = new Padding(20);
            layout.Controls.Add(stats, 1, 1);

            performanceCpu = NewStatLabel(stats, 0);
            performanceMemory = NewStatLabel(stats, 42);
            performanceNetwork = NewStatLabel(stats, 84);
            performanceSystem = NewStatLabel(stats, 126);
            pages["Performance"] = page;
        }

        private Panel NewGraphPanel(string title, out Label valueLabel)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = SurfaceRaised;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Control c = (Control)sender;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, c.ClientSize.Width - 1, c.ClientSize.Height - 1);
            };
            Label label = new Label();
            label.Text = title;
            label.Font = new Font("Segoe UI Semibold", 11F);
            label.ForeColor = TextMain;
            label.AutoSize = true;
            label.Location = new Point(16, 12);
            panel.Controls.Add(label);

            Label graphValue = new Label();
            graphValue.Text = "0%";
            graphValue.Font = new Font("Segoe UI Semibold", 18F);
            graphValue.ForeColor = title == "CPU" ? Accent : Color.FromArgb(151, 96, 255);
            graphValue.AutoSize = false;
            graphValue.Size = new Size(90, 35);
            graphValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            graphValue.Location = new Point(Math.Max(16, panel.ClientSize.Width - 106), 8);
            graphValue.TextAlign = ContentAlignment.MiddleRight;
            panel.Controls.Add(graphValue);
            panel.Resize += delegate
            {
                graphValue.Left = Math.Max(16, panel.ClientSize.Width - graphValue.Width - 16);
            };
            valueLabel = graphValue;
            return panel;
        }

        private Label NewStatLabel(Control parent, int top)
        {
            Label label = new Label();
            label.Location = new Point(18, 18 + top);
            label.Size = new Size(440, 34);
            label.Font = new Font("Segoe UI", 10F);
            label.ForeColor = TextMain;
            parent.Controls.Add(label);
            return label;
        }

        private void BuildStartupPage()
        {
            Panel page = NewPage();
            Panel actions = NewActionBar();
            Button enable = MakeToolbarButton("Enable", 90);
            enable.Location = new Point(12, 10);
            enable.Click += delegate { SetSelectedStartupState(true); };
            actions.Controls.Add(enable);
            Button disable = MakeToolbarButton("Disable", 90);
            disable.Location = new Point(111, 10);
            disable.Click += delegate { SetSelectedStartupState(false); };
            actions.Controls.Add(disable);
            Button refresh = MakeToolbarButton("Refresh", 90);
            refresh.Location = new Point(210, 10);
            refresh.Click += delegate { RefreshStartup(); };
            actions.Controls.Add(refresh);
            page.Controls.Add(actions);

            startupGrid = NewGrid();
            AddTextColumn(startupGrid, "Name", "Name", 220);
            AddTextColumn(startupGrid, "Publisher", "Source", 150);
            AddTextColumn(startupGrid, "Status", "Status", 90);
            AddTextColumn(startupGrid, "Command", "Command", 500);
            startupGrid.Dock = DockStyle.Fill;
            page.Controls.Add(startupGrid);
            page.Controls.SetChildIndex(startupGrid, 0);
            page.Controls.SetChildIndex(actions, 1);
            pages["Startup apps"] = page;
        }

        private void BuildUsersPage()
        {
            Panel page = NewPage();
            Panel actions = NewActionBar();
            Button logoff = MakeToolbarButton("Log off", 100);
            logoff.Location = new Point(12, 10);
            logoff.Click += delegate { LogOffSelectedUser(); };
            actions.Controls.Add(logoff);
            Label permission = new Label();
            permission.Text = IsAdministrator() ? "Administrator controls enabled" : "Run as administrator to log off other users";
            permission.ForeColor = IsAdministrator() ? Color.FromArgb(67, 215, 145) : TextMuted;
            permission.AutoSize = true;
            permission.Location = new Point(130, 20);
            actions.Controls.Add(permission);
            page.Controls.Add(actions);

            usersGrid = NewGrid();
            AddTextColumn(usersGrid, "User", "User", 240);
            AddTextColumn(usersGrid, "Session", "Session ID", 100);
            AddTextColumn(usersGrid, "Status", "Status", 110);
            AddTextColumn(usersGrid, "Processes", "Processes", 110);
            AddTextColumn(usersGrid, "Memory", "Memory", 130);
            usersGrid.Dock = DockStyle.Fill;
            page.Controls.Add(usersGrid);
            page.Controls.SetChildIndex(usersGrid, 0);
            page.Controls.SetChildIndex(actions, 1);
            pages["Users"] = page;
        }

        private void BuildServicesPage()
        {
            Panel page = NewPage();
            Panel actions = NewActionBar();
            page.Controls.Add(actions);

            Button start = MakeToolbarButton("Start", 85);
            start.Location = new Point(12, 10);
            start.Click += delegate { ChangeServiceState(true); };
            actions.Controls.Add(start);
            Button stop = MakeToolbarButton("Stop", 85);
            stop.Location = new Point(106, 10);
            stop.Click += delegate { ChangeServiceState(false); };
            actions.Controls.Add(stop);
            Button restart = MakeToolbarButton("Restart", 90);
            restart.Location = new Point(200, 10);
            restart.Click += delegate { RestartSelectedService(); };
            actions.Controls.Add(restart);
            Button refresh = MakeToolbarButton("Refresh", 85);
            refresh.Location = new Point(299, 10);
            refresh.Click += delegate { RefreshServices(); };
            actions.Controls.Add(refresh);

            serviceGrid = NewGrid();
            AddTextColumn(serviceGrid, "Name", "Name", 210);
            AddTextColumn(serviceGrid, "DisplayName", "Description", 360);
            AddTextColumn(serviceGrid, "Status", "Status", 120);
            AddTextColumn(serviceGrid, "Type", "Service type", 180);
            serviceGrid.Dock = DockStyle.Fill;
            page.Controls.Add(serviceGrid);
            page.Controls.SetChildIndex(serviceGrid, 0);
            page.Controls.SetChildIndex(actions, 1);
            pages["Services"] = page;
        }

        private Panel NewActionBar()
        {
            Panel actions = new Panel();
            actions.Dock = DockStyle.Top;
            actions.Height = 58;
            actions.Padding = new Padding(12, 10, 12, 10);
            actions.BackColor = SurfaceRaised;
            actions.Paint += delegate(object sender, PaintEventArgs e)
            {
                Control c = (Control)sender;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, 0, c.ClientSize.Height - 1, c.ClientSize.Width, c.ClientSize.Height - 1);
            };
            return actions;
        }

        private DataGridView NewGrid()
        {
            DataGridView grid = new DataGridView();
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Border;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.ColumnHeadersHeight = 42;
            grid.RowTemplate.Height = 38;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(10, 15, 39);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AccentBright;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(10, 15, 39);
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextMain;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 55, 115);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(17, 24, 55);
            grid.DefaultCellStyle.NullValue = "—";
            grid.SortCompare += GridSortCompare;
            grid.ColumnHeaderMouseClick += delegate(object sender, DataGridViewCellMouseEventArgs e)
            {
                DataGridView clickedGrid = (DataGridView)sender;
                BeginInvoke(new MethodInvoker(delegate { RememberGridSort(clickedGrid); }));
            };
            return grid;
        }

        private static void AddTextColumn(DataGridView grid, string name, string title, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = title;
            column.Width = width;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            grid.Columns.Add(column);
        }

        private void GridSortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            string columnName = e.Column.Name;
            if (columnName == "PID" || columnName == "Threads" || columnName == "User" ||
                columnName == "Session" || columnName == "Processes")
            {
                e.SortResult = ParseNumber(e.CellValue1).CompareTo(ParseNumber(e.CellValue2));
                e.Handled = true;
            }
            else if (columnName == "CPU")
            {
                e.SortResult = ParsePercent(e.CellValue1).CompareTo(ParsePercent(e.CellValue2));
                e.Handled = true;
            }
            else if (columnName == "Memory")
            {
                e.SortResult = ParseByteValue(e.CellValue1).CompareTo(ParseByteValue(e.CellValue2));
                e.Handled = true;
            }

            if (e.Handled && e.SortResult == 0)
                e.SortResult = e.RowIndex1.CompareTo(e.RowIndex2);
        }

        private static long ParseNumber(object value)
        {
            long result;
            return Int64.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        private static double ParsePercent(object value)
        {
            double result;
            string text = Convert.ToString(value).Replace("%", "").Trim();
            return Double.TryParse(text, out result) ? result : 0;
        }

        private static double ParseByteValue(object value)
        {
            string text = Convert.ToString(value).Trim();
            if (text.Length == 0) return 0;
            string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            double number;
            if (parts.Length == 0 || !Double.TryParse(parts[0], out number)) return 0;
            string unit = parts.Length > 1 ? parts[1].ToUpperInvariant() : "B";
            if (unit == "KB") return number * 1024D;
            if (unit == "MB") return number * 1024D * 1024D;
            if (unit == "GB") return number * 1024D * 1024D * 1024D;
            if (unit == "TB") return number * 1024D * 1024D * 1024D * 1024D;
            return number;
        }

        private void RememberGridSort(DataGridView grid)
        {
            if (grid.SortedColumn == null || grid.SortOrder == SortOrder.None) return;
            GridSortState state = new GridSortState();
            state.ColumnName = grid.SortedColumn.Name;
            state.Direction = grid.SortOrder == SortOrder.Descending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            gridSortStates[grid] = state;
        }

        private void RestoreGridSort(DataGridView grid)
        {
            GridSortState state;
            if (!gridSortStates.TryGetValue(grid, out state)) return;
            if (!grid.Columns.Contains(state.ColumnName)) return;
            try { grid.Sort(grid.Columns[state.ColumnName], state.Direction); }
            catch { }
        }

        private void ShowPage(string name)
        {
            activePage = name;
            pageTitle.Text = name;
            pageSubtitle.Text = pageDescriptions.ContainsKey(name) ? pageDescriptions[name] : "";
            foreach (KeyValuePair<string, Control> item in pages)
                item.Value.Visible = item.Key == name;
            foreach (KeyValuePair<string, Button> item in navButtons)
            {
                bool selected = item.Key == name;
                item.Value.BackColor = selected ? Color.FromArgb(24, 43, 95) : NavBack;
                item.Value.ForeColor = selected ? Color.White : TextMuted;
                item.Value.FlatAppearance.BorderSize = selected ? 1 : 0;
                item.Value.FlatAppearance.BorderColor = selected ? Accent : NavBack;
                item.Value.Font = new Font("Segoe UI", 9F, selected ? FontStyle.Bold : FontStyle.Regular);
            }

            bool processActions = name == "Processes" || name == "Details";
            searchBox.Visible = processActions;
            runTaskButton.Visible = processActions;
            endTaskButton.Visible = processActions;
            endAllButton.Visible = processActions;
            efficiencyButton.Visible = processActions;
            search.Enabled = processActions;
            search.BackColor = processActions ? SurfaceRaised : Color.FromArgb(15, 20, 43);
            search.ForeColor = processActions ? TextMain : TextMuted;

            if (name == "Startup apps") RefreshStartup();
            if (name == "Users") RefreshUsers();
            if (name == "Services") RefreshServices();
            if (name == "Processes" || name == "Details") RefreshProcesses();
        }

        private void RefreshAll(bool force)
        {
            tickCount++;
            UpdatePerformance();
            if (activePage == "Processes" || activePage == "Details" || force)
                RefreshProcesses();
            if (activePage == "Users" && (tickCount % 3 == 0 || force))
                RefreshUsers();
            if (activePage == "Services" && (tickCount % 5 == 0 || force))
                RefreshServices();
        }

        private void RefreshProcesses()
        {
            DateTime now = DateTime.UtcNow;
            double seconds = Math.Max(0.1, (now - lastProcessSample).TotalSeconds);
            lastProcessSample = now;
            int selectedPid = GetSelectedPid();
            string filter = search.Text.Trim();
            List<ProcessRow> rows = new List<ProcessRow>();
            Dictionary<int, TimeSpan> currentCpu = new Dictionary<int, TimeSpan>();
            double totalCpu = 0;
            long totalMemory = 0;

            Process[] processes;
            try { processes = Process.GetProcesses(); }
            catch { return; }

            foreach (Process process in processes)
            {
                try
                {
                    string name = process.ProcessName;
                    int pid = process.Id;
                    TimeSpan cpuTime = process.TotalProcessorTime;
                    currentCpu[pid] = cpuTime;
                    double cpu = 0;
                    TimeSpan oldCpu;
                    if (processCpu.TryGetValue(pid, out oldCpu))
                        cpu = Math.Max(0, Math.Min(100, (cpuTime - oldCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100));

                    long memory = process.WorkingSet64;
                    totalCpu += cpu;
                    totalMemory += memory;
                    string description = SafeDescription(process);
                    if (filter.Length > 0 &&
                        name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        pid.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    ProcessRow row = new ProcessRow();
                    row.Name = name;
                    row.Pid = pid;
                    row.Cpu = cpu;
                    row.Memory = memory;
                    row.Threads = SafeThreadCount(process);
                    row.Description = description;
                    row.Session = SafeSessionId(process);
                    row.Priority = SafePriority(process);
                    row.Path = SafePath(process);
                    rows.Add(row);
                }
                catch { }
                finally { process.Dispose(); }
            }

            processCpu.Clear();
            foreach (KeyValuePair<int, TimeSpan> item in currentCpu)
                processCpu[item.Key] = item.Value;

            rows.Sort(delegate(ProcessRow a, ProcessRow b) { return b.Cpu.CompareTo(a.Cpu); });
            FillProcessGrid(processGrid, rows, false, selectedPid);
            FillProcessGrid(detailGrid, rows, true, selectedPid);
            processSummary.Text = String.Format("LIVE   •   {0} processes   •   CPU {1:0}%   •   Memory {2}",
                processes.Length, totalCpu, FormatBytes(totalMemory));
        }

        private void FillProcessGrid(DataGridView grid, List<ProcessRow> rows, bool details, int selectedPid)
        {
            if (grid == null) return;
            RememberGridSort(grid);
            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (ProcessRow item in rows)
            {
                int index;
                if (details)
                {
                    index = grid.Rows.Add(item.Name + ".exe", item.Pid, "Running", item.Session,
                        item.Cpu.ToString("0.0") + "%", FormatBytes(item.Memory), item.Threads,
                        item.Priority, item.Path);
                }
                else
                {
                    index = grid.Rows.Add(item.Name, item.Pid, "Running", item.Cpu.ToString("0.0") + "%",
                        FormatBytes(item.Memory), item.Threads, item.Description);
                }
                grid.Rows[index].Tag = item.Pid;
                if (item.Cpu >= 20)
                {
                    grid.Rows[index].Cells["CPU"].Style.BackColor = Color.FromArgb(100, 31, 72);
                    grid.Rows[index].Cells["CPU"].Style.ForeColor = Color.FromArgb(255, 207, 222);
                }
                else if (item.Cpu >= 5)
                {
                    grid.Rows[index].Cells["CPU"].Style.BackColor = Color.FromArgb(34, 53, 111);
                    grid.Rows[index].Cells["CPU"].Style.ForeColor = AccentBright;
                }
            }
            RestoreGridSort(grid);
            RestoreSelection(grid, selectedPid);
            grid.ResumeLayout();
        }

        private static void RestoreSelection(DataGridView grid, int pid)
        {
            if (pid < 0) return;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is int && (int)row.Tag == pid)
                {
                    row.Selected = true;
                    if (row.Cells.Count > 0) grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }

        private int GetSelectedPid()
        {
            DataGridView grid = activePage == "Details" ? detailGrid : processGrid;
            if (grid != null && grid.SelectedRows.Count > 0 && grid.SelectedRows[0].Tag is int)
                return (int)grid.SelectedRows[0].Tag;
            return -1;
        }

        private void EndSelectedProcess()
        {
            int pid = GetSelectedPid();
            if (pid < 0)
            {
                MessageBox.Show(this, "Select a process first.", "Kappy Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (pid == Process.GetCurrentProcess().Id)
            {
                MessageBox.Show(this, "Kappy Manager cannot end itself from this view.", "Kappy Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult result = MessageBox.Show(this,
                "End process " + pid + "?\r\n\r\nUnsaved data in that process may be lost.",
                "End task", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    process.Kill();
                RefreshProcesses();
            }
            catch (Exception ex)
            {
                ShowOperationError("The process could not be ended.", ex);
            }
        }

        private void EndAllMatchingProcesses()
        {
            int pid = GetSelectedPid();
            if (pid < 0)
            {
                MessageBox.Show(this, "Select a process first.", "Kappy Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string processName;
            try
            {
                using (Process selected = Process.GetProcessById(pid))
                    processName = selected.ProcessName;
            }
            catch (Exception ex)
            {
                ShowOperationError("The selected process is no longer available.", ex);
                RefreshProcesses();
                return;
            }

            Process[] matches;
            try { matches = Process.GetProcessesByName(processName); }
            catch (Exception ex)
            {
                ShowOperationError("Matching processes could not be found.", ex);
                return;
            }

            int currentPid = Process.GetCurrentProcess().Id;
            List<Process> targets = new List<Process>();
            foreach (Process process in matches)
            {
                if (process.Id == currentPid)
                {
                    process.Dispose();
                    continue;
                }
                targets.Add(process);
            }

            if (targets.Count == 0)
            {
                MessageBox.Show(this, "There are no matching processes that Kappy Manager can end.",
                    "End all", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(this,
                String.Format("End all {0} running processes named {1}.exe?\r\n\r\nUnsaved data in any of these processes may be lost.",
                    targets.Count, processName),
                "End all matching processes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                foreach (Process process in targets) process.Dispose();
                return;
            }

            int ended = 0;
            List<string> failures = new List<string>();
            foreach (Process process in targets)
            {
                try
                {
                    int targetPid = process.Id;
                    process.Kill();
                    process.WaitForExit(2000);
                    ended++;
                }
                catch (Exception ex)
                {
                    failures.Add(process.Id + ": " + ex.Message);
                }
                finally { process.Dispose(); }
            }

            RefreshProcesses();
            if (failures.Count > 0)
            {
                string details = String.Join("\r\n", failures.Take(5).ToArray());
                if (failures.Count > 5)
                    details += "\r\n…and " + (failures.Count - 5) + " more.";
                MessageBox.Show(this,
                    String.Format("Ended {0} process(es). {1} could not be ended.\r\n\r\n{2}\r\n\r\nRun Kappy Manager as administrator if access was denied.",
                        ended, failures.Count, details),
                    "End all completed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetEfficiencyMode()
        {
            int pid = GetSelectedPid();
            if (pid < 0)
            {
                MessageBox.Show(this, "Select a process first.", "Kappy Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                MessageBox.Show(this, "Process priority was set to Below normal.", "Efficiency mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshProcesses();
            }
            catch (Exception ex)
            {
                ShowOperationError("Efficiency mode could not be applied.", ex);
            }
        }

        private void FocusSelectedProcess()
        {
            int pid = GetSelectedPid();
            if (pid < 0) return;
            ShowPage("Details");
            RestoreSelection(detailGrid, pid);
        }

        private void RunNewTask()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "Create new task";
                dialog.Icon = Icon;
                dialog.Size = new Size(520, 190);
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Font = Font;

                Label prompt = new Label();
                prompt.Text = "Enter the name of a program, folder, document, or Internet resource:";
                prompt.AutoSize = true;
                prompt.Location = new Point(18, 18);
                dialog.Controls.Add(prompt);
                TextBox command = new TextBox();
                command.Location = new Point(20, 48);
                command.Size = new Size(467, 25);
                dialog.Controls.Add(command);
                Button browse = new Button();
                browse.Text = "Browse…";
                browse.Location = new Point(211, 92);
                browse.Size = new Size(85, 30);
                browse.Click += delegate
                {
                    using (OpenFileDialog picker = new OpenFileDialog())
                    {
                        picker.Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*";
                        if (picker.ShowDialog(dialog) == DialogResult.OK) command.Text = picker.FileName;
                    }
                };
                dialog.Controls.Add(browse);
                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(307, 92);
                cancel.Size = new Size(85, 30);
                dialog.Controls.Add(cancel);
                Button ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(402, 92);
                ok.Size = new Size(85, 30);
                dialog.Controls.Add(ok);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) == DialogResult.OK && command.Text.Trim().Length > 0)
                {
                    try
                    {
                        ProcessStartInfo info = new ProcessStartInfo(command.Text.Trim());
                        info.UseShellExecute = true;
                        Process.Start(info);
                    }
                    catch (Exception ex) { ShowOperationError("The task could not be started.", ex); }
                }
            }
        }

        private void UpdatePerformance()
        {
            float cpu = GetSystemCpu();
            MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
            memory.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref memory);
            float memoryPercent = memory.ullTotalPhys == 0 ? 0 : (float)((memory.ullTotalPhys - memory.ullAvailPhys) * 100.0 / memory.ullTotalPhys);
            AddHistory(cpuHistory, cpu);
            AddHistory(memoryHistory, memoryPercent);
            cpuGraphValue.Text = cpu.ToString("0") + "%";
            memoryGraphValue.Text = memoryPercent.ToString("0") + "%";
            cpuGraph.Invalidate();
            memoryGraph.Invalidate();

            long networkBytes = GetNetworkBytes();
            DateTime now = DateTime.UtcNow;
            double elapsed = Math.Max(0.1, (now - previousNetworkTime).TotalSeconds);
            double networkRate = previousNetworkBytes == 0 ? 0 : Math.Max(0, (networkBytes - previousNetworkBytes) / elapsed);
            previousNetworkBytes = networkBytes;
            previousNetworkTime = now;

            performanceCpu.Text = String.Format("CPU     {0:0}%   {1} logical processors", cpu, Environment.ProcessorCount);
            performanceMemory.Text = String.Format("Memory  {0:0}%   {1} / {2}", memoryPercent,
                FormatBytes((long)(memory.ullTotalPhys - memory.ullAvailPhys)), FormatBytes((long)memory.ullTotalPhys));
            performanceNetwork.Text = String.Format("Network  {0}/s", FormatBytes((long)networkRate));
            performanceSystem.Text = String.Format("System   Up time {0}", FormatDuration(TimeSpan.FromMilliseconds(Environment.TickCount & Int32.MaxValue)));
        }

        private static void AddHistory(List<float> list, float value)
        {
            list.Add(value);
            while (list.Count > 90) list.RemoveAt(0);
        }

        private void DrawGraph(Graphics graphics, Panel panel, List<float> history, Color color)
        {
            Rectangle bounds = new Rectangle(16, 58, Math.Max(10, panel.ClientSize.Width - 32), Math.Max(10, panel.ClientSize.Height - 76));
            using (Pen gridPen = new Pen(Color.FromArgb(43, 57, 105)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = bounds.Top + i * bounds.Height / 4;
                    graphics.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
                }
                for (int i = 0; i <= 8; i++)
                {
                    int x = bounds.Left + i * bounds.Width / 8;
                    graphics.DrawLine(gridPen, x, bounds.Top, x, bounds.Bottom);
                }
            }
            if (history.Count < 2) return;
            PointF[] points = new PointF[history.Count];
            for (int i = 0; i < history.Count; i++)
            {
                float x = bounds.Right - (history.Count - 1 - i) * (bounds.Width / 89F);
                float y = bounds.Bottom - 3 - (bounds.Height - 6) * Math.Max(0, Math.Min(100, history[i])) / 100F;
                points[i] = new PointF(x, y);
            }
            using (Pen line = new Pen(color, 2F))
                graphics.DrawLines(line, points);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                if (activePage != "Processes" && activePage != "Details")
                    ShowPage("Processes");
                search.Focus();
                search.SelectAll();
                return true;
            }
            if (keyData == Keys.F5)
            {
                RefreshAll(true);
                return true;
            }
            if (keyData == Keys.Delete && (activePage == "Processes" || activePage == "Details"))
            {
                EndSelectedProcess();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Delete) &&
                (activePage == "Processes" || activePage == "Details"))
            {
                EndAllMatchingProcesses();
                return true;
            }
            if (keyData == (Keys.Control | Keys.R) &&
                (activePage == "Processes" || activePage == "Details"))
            {
                RunNewTask();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshStartup()
        {
            List<StartupEntry> entries = new List<StartupEntry>();
            ReadStartupKey(entries, false, @"Software\Microsoft\Windows\CurrentVersion\Run", "Current user");
            ReadStartupKey(entries, true, @"Software\Microsoft\Windows\CurrentVersion\Run", "All users");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), false, "Current user");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), true, "All users");
            RememberGridSort(startupGrid);
            startupGrid.Rows.Clear();
            foreach (StartupEntry entry in entries)
            {
                int index = startupGrid.Rows.Add(entry.Name, entry.Source,
                    entry.Enabled ? "Enabled" : "Disabled", entry.Command);
                startupGrid.Rows[index].Tag = entry;
            }
            RestoreGridSort(startupGrid);
        }

        private static void ReadStartupKey(List<StartupEntry> entries, bool machine, string path, string source)
        {
            try
            {
                RegistryKey root = machine ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        StartupEntry entry = new StartupEntry();
                        entry.Name = name;
                        entry.Source = source + " registry";
                        entry.Command = Convert.ToString(key.GetValue(name));
                        entry.Machine = machine;
                        entry.ApprovalPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                        entry.ApprovalValueName = name;
                        entry.Enabled = ReadStartupApproval(entry);
                        entries.Add(entry);
                    }
                }
            }
            catch { }
        }

        private static void ReadStartupFolder(List<StartupEntry> entries, string path, bool machine, string source)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string file in Directory.GetFiles(path))
                {
                    StartupEntry entry = new StartupEntry();
                    entry.Name = Path.GetFileNameWithoutExtension(file);
                    entry.Source = source + " startup folder";
                    entry.Command = file;
                    entry.Machine = machine;
                    entry.ApprovalPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                    entry.ApprovalValueName = Path.GetFileName(file);
                    entry.Enabled = ReadStartupApproval(entry);
                    entries.Add(entry);
                }
            }
            catch { }
        }

        private static bool ReadStartupApproval(StartupEntry entry)
        {
            try
            {
                RegistryKey root = entry.Machine ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.OpenSubKey(entry.ApprovalPath))
                {
                    if (key == null) return true;
                    byte[] value = key.GetValue(entry.ApprovalValueName) as byte[];
                    return value == null || value.Length == 0 || value[0] != 3;
                }
            }
            catch { return true; }
        }

        private void SetSelectedStartupState(bool enabled)
        {
            if (startupGrid.SelectedRows.Count == 0 || !(startupGrid.SelectedRows[0].Tag is StartupEntry))
            {
                MessageBox.Show(this, "Select a startup application first.", "Kappy Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StartupEntry entry = (StartupEntry)startupGrid.SelectedRows[0].Tag;
            if (entry.Enabled == enabled) return;
            try
            {
                RegistryKey root = entry.Machine ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.CreateSubKey(entry.ApprovalPath))
                {
                    byte[] value = new byte[12];
                    value[0] = enabled ? (byte)2 : (byte)3;
                    if (!enabled)
                    {
                        byte[] timestamp = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                        Array.Copy(timestamp, 0, value, 4, timestamp.Length);
                    }
                    key.SetValue(entry.ApprovalValueName, value, RegistryValueKind.Binary);
                }
                RefreshStartup();
            }
            catch (Exception ex)
            {
                ShowOperationError("The startup application state could not be changed.", ex);
            }
        }

        private void RefreshUsers()
        {
            Dictionary<int, UserRow> sessions = new Dictionary<int, UserRow>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    int session = process.SessionId;
                    UserRow row;
                    if (!sessions.TryGetValue(session, out row))
                    {
                        row = new UserRow();
                        row.Session = session;
                        row.Name = session == Process.GetCurrentProcess().SessionId
                            ? Environment.UserDomainName + "\\" + Environment.UserName
                            : "Session " + session;
                        sessions[session] = row;
                    }
                    row.Processes++;
                    row.Memory += process.WorkingSet64;
                }
                catch { }
                finally { process.Dispose(); }
            }
            RememberGridSort(usersGrid);
            usersGrid.Rows.Clear();
            foreach (UserRow row in sessions.Values.OrderBy(delegate(UserRow x) { return x.Session; }))
            {
                string sessionUser = GetSessionUserName(row.Session);
                if (sessionUser.Length > 0) row.Name = sessionUser;
                int index = usersGrid.Rows.Add(row.Name, row.Session,
                    row.Session == Process.GetCurrentProcess().SessionId ? "Active" : "Connected",
                    row.Processes, FormatBytes(row.Memory));
                usersGrid.Rows[index].Tag = row.Session;
            }
            RestoreGridSort(usersGrid);
        }

        private void LogOffSelectedUser()
        {
            if (usersGrid.SelectedRows.Count == 0 || !(usersGrid.SelectedRows[0].Tag is int))
            {
                MessageBox.Show(this, "Select a user session first.", "Kappy Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int sessionId = (int)usersGrid.SelectedRows[0].Tag;
            if (sessionId == Process.GetCurrentProcess().SessionId)
            {
                MessageBox.Show(this, "Kappy Manager will not log off its own active session.",
                    "Log off user", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string user = Convert.ToString(usersGrid.SelectedRows[0].Cells["User"].Value);
            DialogResult answer = MessageBox.Show(this,
                "Log off " + user + "?\r\n\r\nUnsaved work in that session will be lost.",
                "Log off user", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            if (!WTSLogoffSession(IntPtr.Zero, sessionId, false))
            {
                int error = Marshal.GetLastWin32Error();
                ShowOperationError("The user session could not be logged off.", new Win32Exception(error));
                return;
            }
            RefreshUsers();
        }

        private static string GetSessionUserName(int sessionId)
        {
            string user = QuerySessionString(sessionId, WTS_INFO_CLASS.WTSUserName);
            if (user.Length == 0) return "";
            string domain = QuerySessionString(sessionId, WTS_INFO_CLASS.WTSDomainName);
            return domain.Length > 0 ? domain + "\\" + user : user;
        }

        private static string QuerySessionString(int sessionId, WTS_INFO_CLASS infoClass)
        {
            IntPtr buffer = IntPtr.Zero;
            int bytes;
            try
            {
                if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out buffer, out bytes) ||
                    buffer == IntPtr.Zero || bytes <= 1)
                    return "";
                return Marshal.PtrToStringAuto(buffer) ?? "";
            }
            finally
            {
                if (buffer != IntPtr.Zero) WTSFreeMemory(buffer);
            }
        }

        private void RefreshServices()
        {
            int selected = serviceGrid.SelectedRows.Count > 0 ? serviceGrid.SelectedRows[0].Index : -1;
            RememberGridSort(serviceGrid);
            serviceGrid.Rows.Clear();
            try
            {
                ServiceController[] services = ServiceController.GetServices();
                Array.Sort(services, delegate(ServiceController a, ServiceController b) { return String.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase); });
                foreach (ServiceController service in services)
                {
                    try
                    {
                        int index = serviceGrid.Rows.Add(service.ServiceName, service.DisplayName, service.Status, service.ServiceType);
                        serviceGrid.Rows[index].Tag = service.ServiceName;
                    }
                    catch { }
                    finally { service.Dispose(); }
                }
                RestoreGridSort(serviceGrid);
                if (selected >= 0 && selected < serviceGrid.Rows.Count)
                    serviceGrid.Rows[selected].Selected = true;
            }
            catch (Exception ex)
            {
                ShowOperationError("Services could not be read.", ex);
            }
        }

        private void ChangeServiceState(bool start)
        {
            if (serviceGrid.SelectedRows.Count == 0 || !(serviceGrid.SelectedRows[0].Tag is string))
            {
                MessageBox.Show(this, "Select a service first.", "Kappy Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = (string)serviceGrid.SelectedRows[0].Tag;
            DialogResult answer = MessageBox.Show(this, (start ? "Start " : "Stop ") + name + "?",
                "Service control", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                using (ServiceController service = new ServiceController(name))
                {
                    if (start) service.Start();
                    else service.Stop();
                    service.WaitForStatus(start ? ServiceControllerStatus.Running : ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
                RefreshServices();
            }
            catch (Exception ex) { ShowOperationError("The service state could not be changed.", ex); }
        }

        private void RestartSelectedService()
        {
            if (serviceGrid.SelectedRows.Count == 0 || !(serviceGrid.SelectedRows[0].Tag is string))
            {
                MessageBox.Show(this, "Select a service first.", "Kappy Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = (string)serviceGrid.SelectedRows[0].Tag;
            DialogResult answer = MessageBox.Show(this, "Restart " + name + "?",
                "Service control", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                using (ServiceController service = new ServiceController(name))
                {
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Stopped)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    }
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                }
                RefreshServices();
            }
            catch (Exception ex) { ShowOperationError("The service could not be restarted.", ex); }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private void ShowOperationError(string message, Exception ex)
        {
            string detail = ex is Win32Exception && ((Win32Exception)ex).NativeErrorCode == 5
                ? "Access was denied. Run Kappy Manager as administrator if this operation is required."
                : ex.Message;
            MessageBox.Show(this, message + "\r\n\r\n" + detail, "Kappy Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string SafeDescription(Process process)
        {
            try { return process.MainModule.FileVersionInfo.FileDescription ?? ""; }
            catch { return ""; }
        }

        private static string SafePath(Process process)
        {
            try { return process.MainModule.FileName; }
            catch { return ""; }
        }

        private static string SafePriority(Process process)
        {
            try { return process.PriorityClass.ToString(); }
            catch { return ""; }
        }

        private static int SafeThreadCount(Process process)
        {
            try { return process.Threads.Count; }
            catch { return 0; }
        }

        private static int SafeSessionId(Process process)
        {
            try { return process.SessionId; }
            catch { return -1; }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit >= 2 ? "0.0" : "0") + " " + units[unit];
        }

        private static string FormatDuration(TimeSpan value)
        {
            if (value.TotalDays >= 1) return String.Format("{0}d {1}h {2}m", (int)value.TotalDays, value.Hours, value.Minutes);
            return String.Format("{0}h {1}m", (int)value.TotalHours, value.Minutes);
        }

        private long GetNetworkBytes()
        {
            long total = 0;
            try
            {
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (item.OperationalStatus != OperationalStatus.Up || item.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    IPv4InterfaceStatistics stats = item.GetIPv4Statistics();
                    total += stats.BytesReceived + stats.BytesSent;
                }
            }
            catch { }
            return total;
        }

        private void SeedSystemCpu()
        {
            FILETIME idle, kernel, user;
            if (GetSystemTimes(out idle, out kernel, out user))
            {
                previousSystemIdle = FileTimeToLong(idle);
                previousSystemKernel = FileTimeToLong(kernel);
                previousSystemUser = FileTimeToLong(user);
            }
        }

        private float GetSystemCpu()
        {
            FILETIME idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return 0;
            long idleNow = FileTimeToLong(idle);
            long kernelNow = FileTimeToLong(kernel);
            long userNow = FileTimeToLong(user);
            long idleDelta = idleNow - previousSystemIdle;
            long totalDelta = (kernelNow - previousSystemKernel) + (userNow - previousSystemUser);
            previousSystemIdle = idleNow;
            previousSystemKernel = kernelNow;
            previousSystemUser = userNow;
            if (totalDelta <= 0) return 0;
            return (float)Math.Max(0, Math.Min(100, (totalDelta - idleDelta) * 100.0 / totalDelta));
        }

        private static long FileTimeToLong(FILETIME value)
        {
            return ((long)value.dwHighDateTime << 32) + value.dwLowDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSLogoffSession(IntPtr serverHandle, int sessionId, bool wait);

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool WTSQuerySessionInformation(
            IntPtr serverHandle, int sessionId, WTS_INFO_CLASS infoClass,
            out IntPtr buffer, out int bytesReturned);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr memory);

        private enum WTS_INFO_CLASS
        {
            WTSUserName = 5,
            WTSDomainName = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private sealed class ProcessRow
        {
            public string Name;
            public int Pid;
            public double Cpu;
            public long Memory;
            public int Threads;
            public string Description;
            public int Session;
            public string Priority;
            public string Path;
        }

        private sealed class UserRow
        {
            public string Name;
            public int Session;
            public int Processes;
            public long Memory;
        }

        private sealed class StartupEntry
        {
            public string Name;
            public string Source;
            public string Command;
            public bool Machine;
            public string ApprovalPath;
            public string ApprovalValueName;
            public bool Enabled;
        }

        private sealed class GridSortState
        {
            public string ColumnName;
            public ListSortDirection Direction;
        }
    }
}
