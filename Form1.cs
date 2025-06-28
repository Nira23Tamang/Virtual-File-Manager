using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Virtual_File_Manager_Predefense
{
    public partial class Form1 : Form
    {
        #region Fields and Constants

        private TabControl tabControl;
        private Button newTabButton;
        private static bool hasInitialTabSwapRun = false;

        private const int TabButtonWidth = 40;

        readonly string configFilePath = @"C:\Users\niraj\Desktop\Virtual_File_Manager\Virtual_File_Manager_Predefense\loadDefaultDirectory.txt";
        readonly string themeConfigFilePath = @"C:\Users\niraj\Desktop\Virtual_File_Manager\Virtual_File_Manager_Predefense\loadThemeSetting.txt";

        private string defaultDirectory;
        private string defaultTheme;

        private Color lightBackground = Color.FromArgb(173, 216, 230);
        private Color darkBackground = Color.FromArgb(26, 42, 68);
        private Color lightButtonColor = Color.FromArgb(135, 206, 250);
        private Color darkButtonColor = Color.FromArgb(51, 102, 102);
        private Color lightTextColor = Color.Black;
        private Color darkTextColor = Color.White;
        private Color lightActiveTabColor = Color.FromArgb(240, 248, 255);
        private Color lightInactiveTabColor = Color.FromArgb(176, 196, 222);
        private Color darkActiveTabColor = Color.FromArgb(80, 120, 160);
        private Color darkInactiveTabColor = Color.FromArgb(50, 70, 100);

        private string currentFileType = "";

        private Tuple<Color, Color> newTabOriginalColors = null;

        private const int filesPerPage = 12;


        #endregion

        #region Constructor and Initialization
        public Form1()
        {
            this.ClientSize = new Size(854, 480);
            this.Text = "Virtaul File Manger";
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Alignment = TabAlignment.Top,
                ItemSize = new Size(120, 40),
                SizeMode = TabSizeMode.Fixed,
                DrawMode = TabDrawMode.OwnerDrawFixed
            };

            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.MouseDown += TabControl_MouseDown;

            this.Controls.Add(tabControl);
            InitializeNewTabButton();


            LoadThemeSetting();
            LoadDefaultDirectory();

            this.Load += Form_Load;
            this.PerformLayout();
        }
        private void Form_Load(object sender, EventArgs e)
        {
            if (!hasInitialTabSwapRun)
            {
                AddNewTab("Tab 1");

                if (tabControl.TabPages.Count > 0)
                {
                    var currentTab = tabControl.SelectedTab;
                    ShowPaginationControls(currentTab);
                    UpdatePaginationControls(currentTab);
                    PositionPaginationControls(currentTab);
                    currentTab.Refresh();
                }

                tabControl.Refresh();
                PositionNewTabButton();
                hasInitialTabSwapRun = true;
            }
        }
        public string ShowInputDialog(string prompt, string title, string defaultText = "")
        {
            Form promptForm = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false
            };

            Label textLabel = new Label() { Left = 10, Top = 20, Text = prompt, AutoSize = true };
            System.Windows.Forms.TextBox inputBox = new System.Windows.Forms.TextBox() { Left = 10, Top = 50, Width = 360, Text = defaultText };

            Button confirmation = new Button() { Text = "OK", Left = 280, Width = 90, Top = 80, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { promptForm.Close(); };

            promptForm.Controls.Add(textLabel);
            promptForm.Controls.Add(inputBox);
            promptForm.Controls.Add(confirmation);
            promptForm.AcceptButton = confirmation;

            return promptForm.ShowDialog() == DialogResult.OK ? inputBox.Text : null;
        }

        #endregion

        #region Sorting Logic and Events
        private void OnSortButtonClick(TabPage tab, Button sortButton)
        {
            ContextMenuStrip sortMenu = new ContextMenuStrip();

            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null)
            {
                tagDict = new Dictionary<string, object>
                {
                    ["SortBy"] = "Date",
                    ["SortAscending"] = false,
                    ["CurrentFiles"] = new List<string>()
                };
                tab.Tag = tagDict;
            }

            string currentSortBy = tagDict.ContainsKey("SortBy") ? (string)tagDict["SortBy"] : "Date";
            bool sortAscending = tagDict.ContainsKey("SortAscending") ? (bool)tagDict["SortAscending"] : false;

            ToolStripMenuItem nameItem = new ToolStripMenuItem("Name")
            {
                CheckOnClick = true,
                Checked = currentSortBy == "Name"
            };

            ToolStripMenuItem dateItem = new ToolStripMenuItem("Date")
            {
                CheckOnClick = true,
                Checked = currentSortBy == "Date"
            };

            nameItem.Click += (s, e) =>
            {
                if (!nameItem.Checked) return;
                dateItem.Checked = false;
                tagDict["SortBy"] = "Name";
                SortFilesByCurrentCriteria(tab);
                UpdateSortButtonArrow(sortButton, (bool)tagDict["SortAscending"]);
            };

            dateItem.Click += (s, e) =>
            {
                if (!dateItem.Checked) return;
                nameItem.Checked = false;
                tagDict["SortBy"] = "Date";
                SortFilesByCurrentCriteria(tab);
                UpdateSortButtonArrow(sortButton, (bool)tagDict["SortAscending"]);
            };

            ToolStripSeparator separator = new ToolStripSeparator();

            ToolStripMenuItem ascendingItem = new ToolStripMenuItem("Ascending")
            {
                CheckOnClick = true,
                Checked = sortAscending
            };
            ToolStripMenuItem descendingItem = new ToolStripMenuItem("Descending")
            {
                CheckOnClick = true,
                Checked = !sortAscending
            };

            ascendingItem.Click += (s, e) =>
            {
                if (!ascendingItem.Checked) return;
                descendingItem.Checked = false;
                tagDict["SortAscending"] = true;
                SortFilesByCurrentCriteria(tab);
                UpdateSortButtonArrow(sortButton, true);
            };

            descendingItem.Click += (s, e) =>
            {
                if (!descendingItem.Checked) return;
                ascendingItem.Checked = false;
                tagDict["SortAscending"] = false;
                SortFilesByCurrentCriteria(tab);
                UpdateSortButtonArrow(sortButton, false);
            };

            sortMenu.Items.Add(nameItem);
            sortMenu.Items.Add(dateItem);
            sortMenu.Items.Add(separator);
            sortMenu.Items.Add(ascendingItem);
            sortMenu.Items.Add(descendingItem);

            sortMenu.Show(sortButton, new Point(0, sortButton.Height));
        }
        private void SortFilesByCurrentCriteria(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var recentFilesPanel = tagDict["RecentFilesPanel"] as FlowLayoutPanel;
            var fileDisplayPanel = tagDict["FileDisplayPanel"] as FlowLayoutPanel;
            var currentFiles = tagDict["CurrentFiles"] as List<string>;

            if (currentFiles == null || currentFiles.Count == 0) return;

            string sortBy = tagDict["SortBy"] as string ?? "Date";
            bool sortAscending = (bool)tagDict["SortAscending"];

            if (sortBy == "Name")
            {
                currentFiles = sortAscending
                    ? currentFiles.OrderBy(f => Path.GetFileName(f)).ToList()
                    : currentFiles.OrderByDescending(f => Path.GetFileName(f)).ToList();
            }
            else if (sortBy == "Date")
            {
                currentFiles = sortAscending
                    ? currentFiles.OrderBy(f => File.GetLastAccessTimeUtc(f)).ToList()
                    : currentFiles.OrderByDescending(f => File.GetLastAccessTimeUtc(f)).ToList();
            }

            tagDict["CurrentFiles"] = currentFiles;
            var targetPanel = fileDisplayPanel.Visible ? fileDisplayPanel : recentFilesPanel;

            foreach (Control control in targetPanel.Controls)
            {
                if (control is Panel panel)
                {
                    foreach (Control child in panel.Controls)
                    {
                        if (child is PictureBox pictureBox && pictureBox.Image != null)
                        {
                            pictureBox.Image.Dispose();
                            pictureBox.Image = null;
                        }
                        child.Dispose();
                    }
                    panel.Dispose();
                }
            }
            targetPanel.Controls.Clear();

            foreach (string filePath in currentFiles)
            {
                string fileType = GetFileTypeFromExtension(Path.GetExtension(filePath).ToLower());
                AddFileDisplay(targetPanel, filePath, fileType);
            }

            if (!fileDisplayPanel.Visible)
            {
                ShowPaginationControls(tab);
                UpdatePaginationControls(tab);
            }

            targetPanel.Invalidate();
            targetPanel.Update();
            tab.Refresh();
        }
        private void UpdateSortButtonArrow(Button sortButton, bool ascending)
        {
            if (sortButton == null) return;

            string arrow = ascending ? "▲" : "▼";
            sortButton.Text = $"Sort {arrow}";
        }
        private void ShowSortButton(TabPage tab)
        {
            Control sortBtn = tab.Controls.Find("sortButton", false).FirstOrDefault();
            if (sortBtn != null)
                sortBtn.Visible = true;
        }

        #endregion 

        #region Manage Tab
        private void InitializeNewTabButton()
        {
            newTabButton = new Button
            {
                Text = "+",
                Width = TabButtonWidth,
                Height = TabButtonWidth,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightButtonColor,
                ForeColor = Color.Black,
                Font = new Font("Arial", 14, FontStyle.Bold)
            };

            // Attach event handlers
            newTabButton.Click += NewTabButton_Click;
            newTabButton.MouseEnter += NewTabButton_MouseEnter;
            newTabButton.MouseLeave += NewTabButton_MouseLeave;

            this.Controls.Add(newTabButton);
            newTabButton.BringToFront();

            newTabButton.FlatAppearance.BorderColor = Color.Black;
            newTabButton.FlatAppearance.BorderSize = 1;
        }
        private void PositionNewTabButton()
        {
            if (newTabButton != null && tabControl != null)
            {
                int totalTabsWidth = tabControl.ItemSize.Width * tabControl.TabPages.Count;
                newTabButton.Location = new Point(totalTabsWidth, 2);
            }
        }
        private void NewTabButton_MouseEnter(object sender, EventArgs e)
        {
            if (newTabOriginalColors == null)
                newTabOriginalColors = Tuple.Create(newTabButton.BackColor, newTabButton.ForeColor);

            if (this.BackColor == Color.FromArgb(26, 42, 68))
            {
                newTabButton.BackColor = Color.FromArgb(70, 70, 70);
                newTabButton.ForeColor = Color.White;
            }
            else
            {
                newTabButton.BackColor = Color.FromArgb(100, 149, 237);
                newTabButton.ForeColor = Color.Black;
            }

            newTabButton.Cursor = Cursors.Hand;
        }
        private void NewTabButton_MouseLeave(object sender, EventArgs e)
        {
            if (newTabOriginalColors != null)
            {
                newTabButton.BackColor = newTabOriginalColors.Item1;
                newTabButton.ForeColor = newTabOriginalColors.Item2;
            }
            newTabButton.Cursor = Cursors.Default;
        }
        private void NewTabButton_Click(object sender, EventArgs e)
        {
            int tabCount = tabControl.TabPages.Count + 1;
            string tabName = "Tab " + tabCount;
            AddNewTab(tabName);
        }
        private void AddNewTab(string tabName)
        {
            TabPage tab = new TabPage(tabName)
            {
                BackColor = this.BackColor,
                ForeColor = this.ForeColor
            };

            var tagDict = new Dictionary<string, object>
            {
                { "SortBy", "Date" },
                { "SortAscending", false },
                { "CurrentFiles", new List<string>() },
                { "CurrentPage", 1 },
                { "TotalFiles", 0 }
            };
            tab.Tag = tagDict;

            System.Windows.Forms.TextBox searchTextBox = new System.Windows.Forms.TextBox
            {
                Width = 200,
                Height = 35,
                Name = "searchTextBox",
                Text = "Search Files...",
                Font = new Font("Arial", 10, FontStyle.Italic),
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(42, 58, 84) : Color.FromArgb(240, 248, 255),
                ForeColor = Color.Black
            };

            Button searchButton = new Button
            {
                Text = "Search",
                Width = 80,
                Height = 25,
                Name = "searchButton",
                Font = new Font("Arial", 8, FontStyle.Bold),
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250),
                ForeColor = Color.Black
            };

            Color currentButtonThemeColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
            Color currentTextColor = this.BackColor == Color.FromArgb(26, 42, 68) ? darkTextColor : lightTextColor;

            ApplyThemeToControl(searchTextBox, this.BackColor == Color.FromArgb(26, 42, 68), currentButtonThemeColor, currentTextColor);
            ApplyThemeToControl(searchButton, this.BackColor == Color.FromArgb(26, 42, 68), currentButtonThemeColor, currentTextColor);

            searchTextBox.GotFocus += (s, e) =>
            {
                if (searchTextBox.Text == "Search Files...")
                {
                    searchTextBox.Text = "";
                    searchTextBox.Font = new Font("Arial", 10, FontStyle.Regular);
                }
            };
            searchTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchTextBox.Text))
                {
                    searchTextBox.Text = "Search Files...";
                    searchTextBox.Font = new Font("Arial", 10, FontStyle.Italic);
                }
            };

            searchButton.Click += SearchButton_Click;
            searchButton.MouseEnter += SearchButton_MouseEnter;
            searchButton.MouseLeave += SearchButton_MouseLeave;

            tab.Controls.Add(searchTextBox);
            tab.Controls.Add(searchButton);

            

            Button sortButton = new Button
            {
                Text = "Sort ▼",
                Width = 80,
                Height = 25,
                Name = "sortButton",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 8, FontStyle.Regular),
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250),
                ForeColor = Color.Black
            };

            sortButton.MouseEnter += (s, e) =>
            {
                if (this.BackColor == Color.FromArgb(26, 42, 68))
                {
                    sortButton.BackColor = Color.FromArgb(51, 255, 255);
                    sortButton.ForeColor = Color.White;
                }
                else
                {
                    sortButton.BackColor = Color.FromArgb(100, 149, 237);
                    sortButton.ForeColor = Color.Black;
                }
                sortButton.Cursor = Cursors.Hand;
            };

            sortButton.MouseLeave += (s, e) =>
            {
                sortButton.BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
                sortButton.ForeColor = Color.Black;
                sortButton.Cursor = Cursors.Default;
            };

            sortButton.Click += (s, e) => OnSortButtonClick(tab, sortButton);

            tab.Controls.Add(sortButton);
            
            Button hamburgerButton = new Button
            {
                Name = "hamburgerMenuButton",
                Text = "\u2630",
                Font = new Font("Arial", 14, FontStyle.Regular),
                Size = new Size(30, 30),
                Location = new Point(0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250),
                ForeColor = Color.Black,
                TabStop = false,
                Cursor = Cursors.Hand
            };

            hamburgerButton.FlatAppearance.BorderColor = Color.Black;
            hamburgerButton.FlatAppearance.BorderSize = 1;

            hamburgerButton.MouseEnter += (s, e) =>
            {
                if (this.BackColor == Color.FromArgb(26, 42, 68))
                {
                    hamburgerButton.BackColor = Color.FromArgb(51, 255, 255);
                    hamburgerButton.ForeColor = Color.White;
                }
                else
                {
                    hamburgerButton.BackColor = Color.FromArgb(100, 149, 237);
                    hamburgerButton.ForeColor = Color.Black;
                }
                hamburgerButton.Cursor = Cursors.Hand;
            };

            hamburgerButton.MouseLeave += (s, e) =>
            {
                hamburgerButton.BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
                hamburgerButton.ForeColor = Color.Black;
                hamburgerButton.Cursor = Cursors.Default;
            };

            ContextMenuStrip hamburgerMenu = new ContextMenuStrip
            {
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(26, 42, 68) : Color.FromArgb(173, 216, 230)
            };

            ToolStripMenuItem imagesItem = new ToolStripMenuItem("Images")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            imagesItem.Click += (s, e) => OnMediaMenuItemClicked(tab, "Images");

            ToolStripMenuItem videoItem = new ToolStripMenuItem("Video")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            videoItem.Click += (s, e) => OnMediaMenuItemClicked(tab, "Video");

            ToolStripMenuItem musicItem = new ToolStripMenuItem("Music")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            musicItem.Click += (s, e) => OnMediaMenuItemClicked(tab, "Music");

            ToolStripMenuItem documentsItem = new ToolStripMenuItem("Documents")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            documentsItem.Click += (s, e) => OnMediaMenuItemClicked(tab, "Documents");

            ToolStripSeparator separator = new ToolStripSeparator()
            {
                BackColor = hamburgerMenu.BackColor
            };

            ToolStripMenuItem changeDirectoryItem = new ToolStripMenuItem("Change Directory")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            changeDirectoryItem.Click += (s, e) => ChangeDirectory();

            ToolStripMenuItem themeItem = new ToolStripMenuItem("Theme")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            ToolStripMenuItem lightThemeItem = new ToolStripMenuItem("Light Mode")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            ToolStripMenuItem darkThemeItem = new ToolStripMenuItem("Dark Mode")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };

            bool isDarkMode = this.BackColor == Color.FromArgb(26, 42, 68);
            darkThemeItem.Checked = isDarkMode;
            lightThemeItem.Checked = !isDarkMode;

            lightThemeItem.Click += (s, e) =>
            {
                ApplyTheme(false);
                lightThemeItem.Checked = true;
                darkThemeItem.Checked = false;
            };

            darkThemeItem.Click += (s, e) =>
            {
                ApplyTheme(true);
                darkThemeItem.Checked = true;
                lightThemeItem.Checked = false;
            };

            themeItem.DropDownItems.Add(lightThemeItem);
            themeItem.DropDownItems.Add(darkThemeItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit")
            {
                BackColor = hamburgerMenu.BackColor,
                ForeColor = currentTextColor
            };
            exitItem.Click += (s, e) => Application.Exit();

            hamburgerMenu.Items.AddRange(new ToolStripItem[]
            {
                imagesItem,
                videoItem,
                musicItem,
                documentsItem,
                separator,
                themeItem,
                changeDirectoryItem,
                exitItem
            });

            hamburgerButton.ContextMenuStrip = hamburgerMenu;
            hamburgerButton.Click += (s, e) =>
            {
                Point btnScreen = hamburgerButton.PointToScreen(Point.Empty);
                int y = btnScreen.Y + hamburgerButton.Height;
                hamburgerMenu.Show(new Point(btnScreen.X + hamburgerButton.Width + 8, y));
            };

            tab.Controls.Add(hamburgerButton);



            Panel separatorLine = new Panel
            {
                Name = "separatorLine",
                Height = 2,
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(100, 100, 100) : Color.FromArgb(176, 196, 222),
                Width = tab.ClientSize.Width,
                Location = new Point(0, searchButton.Bottom + 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tab.Controls.Add(separatorLine);
            separatorLine.BringToFront();


            FlowLayoutPanel recentFilesPanel = new FlowLayoutPanel
            {
                Name = "recentFilesPanel",
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(10),
                BackColor = this.BackColor,
                Location = new Point(20, separatorLine.Bottom + 10),
                Size = new Size(tab.Width - 40, tab.Height - separatorLine.Bottom - 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = true
            };
            tab.Controls.Add(recentFilesPanel);
            tagDict["RecentFilesPanel"] = recentFilesPanel;

            FlowLayoutPanel fileDisplayPanel = new FlowLayoutPanel
            {
                Name = "fileDisplayPanel",
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(10),
                BackColor = this.BackColor,
                Location = new Point(20, separatorLine.Bottom + 10),
                Size = new Size(tab.Width - 40, tab.Height - separatorLine.Bottom - 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };
            tab.Controls.Add(fileDisplayPanel);
            tagDict["FileDisplayPanel"] = fileDisplayPanel;

            Label pageNumberLabel = new Label
            {
                Name = "pageNumberLabel",
                Size = new Size(100, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(26, 42, 68) : Color.FromArgb(173, 216, 230),
                ForeColor = this.BackColor == Color.FromArgb(26, 42, 68) ? darkTextColor : lightTextColor,
                Visible = true
            };

            Button previousButton = new Button
            {
                Name = "previousButton",
                Text = "Previous",
                Size = new Size(75, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250),
                ForeColor = Color.Black,
                Visible = true
            };
            previousButton.MouseEnter += (s, e) =>
            {
                if (this.BackColor == Color.FromArgb(26, 42, 68))
                {
                    previousButton.BackColor = Color.FromArgb(51, 255, 255);
                    previousButton.ForeColor = Color.White;
                }
                else
                {
                    previousButton.BackColor = Color.FromArgb(100, 149, 237);
                    previousButton.ForeColor = Color.Black;
                }
                previousButton.Cursor = Cursors.Hand;
            };
            previousButton.MouseLeave += (s, e) =>
            {
                previousButton.BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
                previousButton.ForeColor = Color.Black;
                previousButton.Cursor = Cursors.Default;
            };
            previousButton.Click += (s, e) => PreviousButton_Click(tab);

            Button nextButton = new Button
            {
                Name = "nextButton",
                Text = "Next",
                Size = new Size(75, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250),
                ForeColor = Color.Black,
                Visible = true
            };
            nextButton.MouseEnter += (s, e) =>
            {
                if (this.BackColor == Color.FromArgb(26, 42, 68))
                {
                    nextButton.BackColor = Color.FromArgb(51, 255, 255);
                    nextButton.ForeColor = Color.White;
                }
                else
                {
                    nextButton.BackColor = Color.FromArgb(100, 149, 237);
                    nextButton.ForeColor = Color.Black;
                }
                nextButton.Cursor = Cursors.Hand;
            };
            nextButton.MouseLeave += (s, e) =>
            {
                nextButton.BackColor = this.BackColor == Color.FromArgb(26, 42, 68) ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
                nextButton.ForeColor = Color.Black;
                nextButton.Cursor = Cursors.Default;
            };
            nextButton.Click += (s, e) => NextButton_Click(tab);

            tab.Controls.Add(pageNumberLabel);
            tab.Controls.Add(previousButton);
            tab.Controls.Add(nextButton);
            tagDict["PageNumberLabel"] = pageNumberLabel;
            tagDict["PreviousButton"] = previousButton;
            tagDict["NextButton"] = nextButton;

            tabControl.Controls.Add(tab);
            tabControl.SelectedTab = tab;

            SwitchToDefaultFileView();

            PositionNewTabButton();
            PositionSearchComponents(tab);
            PositionSeparatorLine(tab);
            PositionPaginationControls(tab);
            ShowSortButton(tab);

            ShowPaginationControls(tab);
            UpdatePaginationControls(tab);

            tab.Refresh();
            tab.Focus();
        }
        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabPage = tabControl.TabPages[e.Index];
            var tabRect = tabControl.GetTabRect(e.Index);

            bool isSelected = (tabControl.SelectedIndex == e.Index);
            bool isDarkMode = this.BackColor == darkBackground;

            Color backColor = isDarkMode
                ? (isSelected ? darkActiveTabColor : darkInactiveTabColor)
                : (isSelected ? lightActiveTabColor : lightInactiveTabColor);

            using (Brush brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, tabRect);
            }

            Color textColor = isDarkMode ? darkTextColor : lightTextColor;

            using (Font boldFont = new Font(e.Font.FontFamily, 10.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    tabPage.Text,
                    boldFont,
                    tabRect,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int verticalPadding = (tabRect.Height - 15) / 2;
            var closeRect = new Rectangle(tabRect.Right - 20, tabRect.Top + verticalPadding, 15, 15);
            e.Graphics.DrawString("x", new Font("Arial", 12, FontStyle.Bold), Brushes.Red, closeRect);
        }
        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                Rectangle tabRect = tabControl.GetTabRect(i);

                int verticalPadding = (tabRect.Height - 15) / 2;
                Rectangle closeButtonRect = new Rectangle(tabRect.Right - 20, tabRect.Top + verticalPadding, 15, 15);

                if (closeButtonRect.Contains(e.Location))
                {
                    var tab = tabControl.TabPages[i];
                    var tagDict = tab.Tag as Dictionary<string, object>;
                    if (tagDict != null)
                    {
                        (tagDict["RecentFilesPanel"] as FlowLayoutPanel)?.Dispose();
                        (tagDict["FileDisplayPanel"] as FlowLayoutPanel)?.Dispose();
                        (tagDict["PageNumberLabel"] as Label)?.Dispose();
                        (tagDict["PreviousButton"] as Button)?.Dispose();
                        (tagDict["NextButton"] as Button)?.Dispose();
                    }

                    tabControl.TabPages.RemoveAt(i);

                    if (tabControl.TabPages.Count == 0)
                    {
                        Application.Exit();
                    }
                    else
                    {
                        RenameTabs();
                        if (tabControl.SelectedTab != null)
                        {
                            ShowPaginationControls(tabControl.SelectedTab);
                            UpdatePaginationControls(tabControl.SelectedTab);
                            PositionPaginationControls(tabControl.SelectedTab);
                            tabControl.SelectedTab.Refresh();
                        }
                        PositionNewTabButton();
                    }
                    break;
                }
            }
        }
        private void RenameTabs()
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                tabControl.TabPages[i].Text = $"Tab {i + 1}";
            }
            tabControl.Invalidate();
        }
        #endregion

        #region File Operation
        private void OpenFileInViewer(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                Process.Start(filePath);
            }
            else
            {
                MessageBox.Show("The file does not exist.", "Error");
            }
        }
        private void AddFileDisplay(FlowLayoutPanel fileDisplayPanel, string filePath, string fileType)
        {
            int panelWidth = 170;
            int panelHeight = 180;

            Panel filePanel = new Panel
            {
                Size = new Size(panelWidth, panelHeight),
                Margin = new Padding(10),
                BackColor = GetPanelColor(fileType)
            };
            filePanel.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, filePanel.ClientRectangle,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid);
            };

            Control displayControl = CreateFileDisplayControl(filePath, fileType);

            if (displayControl is PictureBox pictureBox)
            {
                pictureBox.Size = new Size(panelWidth, panelHeight - 40);
                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            }
            else
            {
                displayControl.Size = new Size(panelWidth, panelHeight - 40);
            }

            displayControl.MouseDoubleClick += (sender, e) => OpenFileInViewer(filePath);

            var (labelBack, labelFore) = GetLabelColors(fileType);

            Label nameLabel = new Label
            {
                Text = GetFileLabelText(fileType, Path.GetFileName(filePath)),
                AutoSize = false,
                Size = new Size(panelWidth, 40), // Match filePanel width exactly
                Location = new Point(0, panelHeight - 40),
                BackColor = labelBack,
                ForeColor = labelFore,
                Font = new Font("Arial", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter // Center text for better alignment
            };
            // Add Paint event for bold, flat 2-pixel border on nameLabel to match filePanel
            nameLabel.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, nameLabel.ClientRectangle,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid);
            };


            // Store the original background colors for BOTH the panel and the label.
            Color originalPanelColor = filePanel.BackColor;
            Color originalLabelColor = nameLabel.BackColor;

            // Define a single highlight color that will be used for both.
            bool isDarkMode = this.BackColor == darkBackground;
            Color highlightColor = isDarkMode ? Color.FromArgb(100, 120, 160) : Color.LightSteelBlue;

            // Create a single MouseEnter handler to highlight both controls.
            EventHandler mouseEnterHandler = (s, e) =>
            {
                filePanel.BackColor = highlightColor;
                nameLabel.BackColor = highlightColor; 
                filePanel.Cursor = Cursors.Hand;
            };

            // Create a single MouseLeave handler to restore both original colors.
            EventHandler mouseLeaveHandler = (s, e) =>
            {
                // This condition checks if the mouse has truly left the entire component.
                if (!filePanel.ClientRectangle.Contains(filePanel.PointToClient(Cursor.Position)))
                {
                    filePanel.BackColor = originalPanelColor;
                    nameLabel.BackColor = originalLabelColor; // Also restore the label's color
                    filePanel.Cursor = Cursors.Default;
                }
            };

            // Attach these handlers to the panel and all its children to prevent flickering.
            filePanel.MouseEnter += mouseEnterHandler;
            filePanel.MouseLeave += mouseLeaveHandler;

            displayControl.MouseEnter += mouseEnterHandler;
            displayControl.MouseLeave += mouseLeaveHandler;

            nameLabel.MouseEnter += mouseEnterHandler;
            nameLabel.MouseLeave += mouseLeaveHandler;

            foreach (Control child in displayControl.Controls)
            {
                child.MouseEnter += mouseEnterHandler;
                child.MouseLeave += mouseLeaveHandler;
            }


            ContextMenuStrip contextMenu = new ContextMenuStrip
            {
                BackColor = this.BackColor == darkBackground ? darkBackground : lightBackground
            };

            ToolStripMenuItem renameItem = new ToolStripMenuItem("Rename")
            {
                BackColor = contextMenu.BackColor,
                ForeColor = this.ForeColor
            };
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("Delete")
            {
                BackColor = contextMenu.BackColor,
                ForeColor = this.ForeColor
            };
            ToolStripMenuItem directoryItem = new ToolStripMenuItem("Directory")
            {
                BackColor = contextMenu.BackColor,
                ForeColor = this.ForeColor
            };

            contextMenu.Items.AddRange(new ToolStripItem[] { renameItem, deleteItem, directoryItem });

            renameItem.Click += (s, e) => renameFile(filePath, filePanel, nameLabel);
            directoryItem.Click += (s, e) => showDirectoryPath(filePath);
            deleteItem.Click += (s, e) => deleteFile(filePath, filePanel, fileDisplayPanel);

            displayControl.ContextMenuStrip = contextMenu;
            nameLabel.ContextMenuStrip = contextMenu;
            filePanel.ContextMenuStrip = contextMenu;

            filePanel.Controls.Add(displayControl);
            filePanel.Controls.Add(nameLabel);
            fileDisplayPanel.Controls.Add(filePanel);
        }
        
        private Control CreateFileDisplayControl(string filePath, string fileType)
        {
            if (fileType == "Image")
            {
                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    Bitmap imageCopy = new Bitmap(Image.FromStream(stream));
                    PictureBox pictureBox = new PictureBox
                    {
                        Image = imageCopy,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(170, 140),
                        BorderStyle = BorderStyle.None,
                        Tag = filePath
                    };
                    pictureBox.Paint += (s, e) =>
                    {
                        ControlPaint.DrawBorder(e.Graphics, pictureBox.ClientRectangle,
                            Color.Black, 2, ButtonBorderStyle.Solid,
                            Color.Black, 2, ButtonBorderStyle.Solid,
                            Color.Black, 2, ButtonBorderStyle.Solid,
                            Color.Black, 2, ButtonBorderStyle.Solid);
                    };
                    return pictureBox;
                }
            }
            else if (fileType == "Video")
            {
                string videoCoverPath = @"C:\Users\niraj\Desktop\Virtual_File_Manager\Virtual_File_Manager_Predefense\video.png";
                if (System.IO.File.Exists(videoCoverPath))
                {
                    using (var stream = new System.IO.FileStream(videoCoverPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                    {
                        Bitmap videoCover = new Bitmap(Image.FromStream(stream));
                        PictureBox pictureBox = new PictureBox
                        {
                            Image = videoCover,
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Size = new Size(170, 140),
                            BorderStyle = BorderStyle.None,
                            Tag = filePath
                        };
                        pictureBox.Paint += (s, e) =>
                        {
                            ControlPaint.DrawBorder(e.Graphics, pictureBox.ClientRectangle,
                                Color.Black, 2, ButtonBorderStyle.Solid,
                                Color.Black, 2, ButtonBorderStyle.Solid,
                                Color.Black, 2, ButtonBorderStyle.Solid,
                                Color.Black, 2, ButtonBorderStyle.Solid);
                        };
                        return pictureBox;
                    }
                }
                else
                {
                }
            }
            else if (fileType == "Music")
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    if (file.Tag.Pictures.Length > 0)
                    {
                        var picture = file.Tag.Pictures[0];
                        using (var ms = new System.IO.MemoryStream(picture.Data.Data))
                        {
                            Bitmap albumArt = new Bitmap(Image.FromStream(ms));
                            PictureBox pictureBox = new PictureBox
                            {
                                Image = albumArt,
                                SizeMode = PictureBoxSizeMode.Zoom,
                                Size = new Size(170, 140),
                                BorderStyle = BorderStyle.None,
                                Tag = filePath
                            };
                            pictureBox.Paint += (s, e) =>
                            {
                                ControlPaint.DrawBorder(e.Graphics, pictureBox.ClientRectangle,
                                    Color.Black, 2, ButtonBorderStyle.Solid,
                                    Color.Black, 2, ButtonBorderStyle.Solid,
                                    Color.Black, 2, ButtonBorderStyle.Solid,
                                    Color.Black, 2, ButtonBorderStyle.Solid);
                            };
                            return pictureBox;
                        }
                    }
                }
            }
            else if (fileType == "Document")
            {
                string pdfCoverPath = @"C:\Users\niraj\Desktop\Virtual_File_Manager\Virtual_File_Manager_Predefense\document.PNG";
                if (System.IO.File.Exists(pdfCoverPath))
                {
                    using (var stream = new System.IO.FileStream(pdfCoverPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                    {
                        Bitmap pdfCover = new Bitmap(Image.FromStream(stream));
                        PictureBox pictureBox = new PictureBox
                        {
                            Image = pdfCover,
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Size = new Size(170, 140),
                            BorderStyle = BorderStyle.None,
                            Tag = filePath
                        };
                        pictureBox.Paint += (s, e) =>
                        {
                            ControlPaint.DrawBorder(e.Graphics, pictureBox.ClientRectangle,
                                Color.Black, 2, ButtonBorderStyle.Solid,
                                Color.Black, 2, ButtonBorderStyle.Solid,
                                Color.Black, 2, ButtonBorderStyle.Solid,
                                Color.Black, 2, ButtonBorderStyle.Solid);
                        };
                        return pictureBox;
                    }
                }
                else
                {
                }
            }

            Panel defaultPanel = new Panel
            {
                Size = new Size(170, 140),
                BorderStyle = BorderStyle.None,
                BackColor = GetPanelColor(fileType)
            };
            defaultPanel.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, defaultPanel.ClientRectangle,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid,
                    Color.Black, 2, ButtonBorderStyle.Solid);
            };

            Label typeLabel = new Label
            {
                Text = fileType,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = this.BackColor == darkBackground ? darkTextColor : lightTextColor,
                BackColor = Color.Transparent
            };
            defaultPanel.Controls.Add(typeLabel);
            return defaultPanel;
        }
        private string GetFileLabelText(string fileType, string fileName)
        {
            return fileName;
        }
        private Color GetPanelColor(string fileType)
        {
            if (this.BackColor == darkBackground)
            {
                if (fileType == "Music") return Color.FromArgb(42, 61, 102);
                if (fileType == "Video") return Color.FromArgb(90, 37, 39);
                if (fileType == "Document") return Color.FromArgb(45, 77, 43);
                if (fileType == "Image") return Color.FromArgb(76, 0, 76);
            }
            else
            {
                if (fileType == "Music") return Color.FromArgb(176, 224, 230);
                if (fileType == "Video") return Color.FromArgb(175, 238, 238);
                if (fileType == "Document") return Color.FromArgb(224, 255, 255);
                if (fileType == "Image") return Color.FromArgb(240, 248, 255);
            }
            return Color.FromArgb(176, 196, 222);
        }
        private (Color backgroundColor, Color foregroundColor) GetLabelColors(string fileType)
        {
            if (this.BackColor == darkBackground)
            {
                if (fileType == "Music") return (Color.FromArgb(66, 103, 178), Color.White);
                if (fileType == "Video") return (Color.FromArgb(183, 50, 44), Color.White);
                if (fileType == "Document") return (Color.FromArgb(102, 182, 106), Color.White);
                if (fileType == "Image") return (Color.FromArgb(128, 0, 128), Color.White);
            }
            else
            {
                if (fileType == "Music") return (Color.FromArgb(70, 130, 180), Color.White);
                if (fileType == "Video") return (Color.FromArgb(0, 191, 255), Color.White);
                if (fileType == "Document") return (Color.FromArgb(0, 206, 209), Color.Black);
                if (fileType == "Image") return (Color.FromArgb(135, 206, 235), Color.Black);
            }
            return (Color.FromArgb(176, 196, 222), Color.Black);
        }
        private void renameFile(string oldFilePath, Panel filePanel, Label nameLabel)
        {
            string directory = Path.GetDirectoryName(oldFilePath);
            string oldFileName = Path.GetFileName(oldFilePath);

            string input = ShowInputDialog("Enter new name for the file (without extension):", "Rename File", Path.GetFileNameWithoutExtension(oldFileName));

            if (string.IsNullOrWhiteSpace(input)) return;

            string newFileName = input.Trim() + Path.GetExtension(oldFileName);
            string newFilePath = Path.Combine(directory, newFileName);

            if (System.IO.File.Exists(newFilePath))
            {
                MessageBox.Show("A file with that name already exists.", "Rename Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            System.IO.File.Move(oldFilePath, newFilePath);

            nameLabel.Text = GetFileLabelText(GetFileTypeFromExtension(Path.GetExtension(newFilePath).ToLower()), newFileName);

            MessageBox.Show("File renamed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            RefreshCurrentTabFiles();
        }
        private void deleteFile(string filePath, Panel filePanel, FlowLayoutPanel parentPanel)
        {
            var confirmResult = MessageBox.Show($"Are you sure to delete this file?\n{filePath}",
                                                "Confirm Delete",
                                                MessageBoxButtons.YesNo,
                                                MessageBoxIcon.Warning);
            if (confirmResult == DialogResult.Yes)
            {
                System.IO.File.Delete(filePath);
                parentPanel.Controls.Remove(filePanel);
                filePanel.Dispose();

                MessageBox.Show("File deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                RefreshCurrentTabFiles();
            }
        }

        private void showDirectoryPath(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                MessageBox.Show("The file does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Form pathDialog = new Form
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Directory Path",
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = this.BackColor == darkBackground ? darkBackground : lightBackground
            };

            Label promptLabel = new Label
            {
                Left = 10,
                Top = 20,
                Text = "File Location:",
                AutoSize = true,
                ForeColor = this.BackColor == darkBackground ? darkTextColor : lightTextColor,
                Padding = new Padding(0, 0, 0, 5)
            };

            System.Windows.Forms.TextBox pathTextBox = new System.Windows.Forms.TextBox
            {
                Left = 10,
                Top = 40,
                Width = 460,
                Text = filePath,
                ReadOnly = true,
                BackColor = this.BackColor == darkBackground ? Color.FromArgb(80, 100, 120) : Color.FromArgb(240, 245, 255),
                ForeColor = this.BackColor == darkBackground ? darkTextColor : lightTextColor
            };
            pathTextBox.SelectAll();

            pathTextBox.Paint += (s, e) =>
            {
                if (this.BackColor == darkBackground)
                {
                    ControlPaint.DrawBorder(e.Graphics, pathTextBox.ClientRectangle,
                        Color.White, 1, ButtonBorderStyle.Solid,
                        Color.White, 1, ButtonBorderStyle.Solid,
                        Color.White, 1, ButtonBorderStyle.Solid,
                        Color.White, 1, ButtonBorderStyle.Solid);
                }
            };

            Button okButton = new Button
            {
                Text = "OK",
                Left = 380,
                Width = 90,
                Top = 80,
                DialogResult = DialogResult.OK,
                BackColor = this.BackColor == darkBackground ? darkButtonColor : lightButtonColor,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            okButton.FlatAppearance.BorderColor = this.BackColor == darkBackground ? Color.White : Color.Black;
            okButton.FlatAppearance.BorderSize = 1;
            okButton.Click += (sender, e) => { pathDialog.Close(); };

            pathDialog.Controls.Add(promptLabel);
            pathDialog.Controls.Add(pathTextBox);
            pathDialog.Controls.Add(okButton);
            pathDialog.AcceptButton = okButton;

            pathDialog.ShowDialog();
        }
        #endregion

        #region Theme Change
        private void LoadThemeSetting()
        {
            if (System.IO.File.Exists(themeConfigFilePath))
            {
                defaultTheme = System.IO.File.ReadAllText(themeConfigFilePath).Trim();
                ApplyTheme(defaultTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                ApplyTheme(false);
            }
        }
        private void ApplyTheme(bool isDarkMode)
        {
            System.IO.File.WriteAllText(themeConfigFilePath, isDarkMode ? "Dark" : "Light");

            Color backColor = isDarkMode ? Color.FromArgb(26, 42, 68) : Color.FromArgb(173, 216, 230);
            Color buttonColor = isDarkMode ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
            Color textColor = isDarkMode ? darkTextColor : lightTextColor;

            this.BackColor = backColor;
            this.ForeColor = textColor;

            if (tabControl != null)
            {
                tabControl.BackColor = backColor;
                tabControl.ForeColor = textColor;

                foreach (TabPage tab in tabControl.TabPages)
                {
                    tab.BackColor = backColor;
                    tab.ForeColor = textColor;

                    var tagDict = tab.Tag as Dictionary<string, object>;
                    if (tagDict != null)
                    {
                        var recentFilesPanel = tagDict.ContainsKey("RecentFilesPanel") ? tagDict["RecentFilesPanel"] as FlowLayoutPanel : null;
                        var fileDisplayPanel = tagDict.ContainsKey("FileDisplayPanel") ? tagDict["FileDisplayPanel"] as FlowLayoutPanel : null;

                        if (recentFilesPanel != null)
                        {
                            recentFilesPanel.BackColor = backColor;
                            foreach (Control filePanel in recentFilesPanel.Controls)
                            {
                                ApplyThemeToControl(filePanel, isDarkMode, buttonColor, textColor);
                                foreach (Control fileControl in filePanel.Controls)
                                {
                                    ApplyThemeToControl(fileControl, isDarkMode, buttonColor, textColor);
                                }
                            }
                        }

                        if (fileDisplayPanel != null)
                        {
                            fileDisplayPanel.BackColor = backColor;
                            foreach (Control filePanel in fileDisplayPanel.Controls)
                            {
                                ApplyThemeToControl(filePanel, isDarkMode, buttonColor, textColor);
                                foreach (Control fileControl in filePanel.Controls)
                                {
                                    ApplyThemeToControl(fileControl, isDarkMode, buttonColor, textColor);
                                }
                            }
                        }

                        ApplyThemeToPaginationControls(tab);
                    }

                    foreach (Control control in tab.Controls)
                    {
                        if (control.Name != "recentFilesPanel" && control.Name != "fileDisplayPanel")
                            ApplyThemeToControl(control, isDarkMode, buttonColor, textColor);

                        if (control is Button btn && btn.ContextMenuStrip != null)
                        {
                            ContextMenuStrip cms = btn.ContextMenuStrip;
                            cms.BackColor = backColor;
                            foreach (ToolStripItem item in cms.Items)
                            {
                                if (item is ToolStripMenuItem menuItem)
                                {
                                    menuItem.BackColor = backColor;
                                    menuItem.ForeColor = textColor;
                                    if (menuItem.DropDownItems.Count > 0)
                                    {
                                        foreach (ToolStripItem subItem in menuItem.DropDownItems)
                                        {
                                            subItem.BackColor = backColor;
                                            subItem.ForeColor = textColor;
                                        }
                                    }
                                }
                                else if (item is ToolStripSeparator separator)
                                {
                                    separator.BackColor = backColor;
                                }
                            }
                        }
                        else if (control is Panel filePanel && filePanel.ContextMenuStrip != null)
                        {
                            ContextMenuStrip cms = filePanel.ContextMenuStrip;
                            cms.BackColor = backColor;
                            foreach (ToolStripItem item in cms.Items)
                            {
                                if (item is ToolStripMenuItem menuItem)
                                {
                                    menuItem.BackColor = backColor;
                                    menuItem.ForeColor = textColor;
                                }
                                else if (item is ToolStripSeparator separator)
                                {
                                    separator.BackColor = backColor;
                                }
                            }
                        }
                        else if (control is PictureBox pictureBox && pictureBox.ContextMenuStrip != null)
                        {
                            ContextMenuStrip cms = pictureBox.ContextMenuStrip;
                            cms.BackColor = backColor;
                            foreach (ToolStripItem item in cms.Items)
                            {
                                if (item is ToolStripMenuItem menuItem)
                                {
                                    menuItem.BackColor = backColor;
                                    menuItem.ForeColor = textColor;
                                }
                                else if (item is ToolStripSeparator separator)
                                {
                                    separator.BackColor = backColor;
                                }
                            }
                        }
                    }
                }
            }

            if (newTabButton != null)
            {
                newTabButton.BackColor = buttonColor;
                newTabButton.ForeColor = isDarkMode ? Color.White : Color.Black;
                newTabButton.FlatAppearance.BorderColor = isDarkMode ? Color.White : Color.Black;
                newTabButton.FlatAppearance.BorderSize = 1;
            }

            foreach (TabPage tab in tabControl.TabPages)
            {
                PositionSearchComponents(tab);
                PositionSeparatorLine(tab);
                PositionPaginationControls(tab);
            }

            this.Invalidate(true);
            this.Update();
        }
        private void ApplyThemeToControl(Control control, bool isDarkMode, Color buttonColor, Color textColor)
        {
            if (control is Button button)
            {
                button.BackColor = buttonColor;
                button.ForeColor = isDarkMode ? Color.White : Color.Black;
                button.Tag = Tuple.Create(button.BackColor, button.ForeColor);
                button.FlatAppearance.BorderColor = isDarkMode ? Color.White : Color.Black;
                button.FlatAppearance.BorderSize = 1;
            }
            else if (control is System.Windows.Forms.TextBox textBox)
            {
                textBox.BackColor = isDarkMode ? Color.FromArgb(42, 58, 84) : Color.FromArgb(240, 248, 255);
                textBox.ForeColor = isDarkMode ? Color.White : Color.Black;
            }
            else if (control is Label label)
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = textColor;
            }
            else if (control is Panel panel)
            {
                if (panel.Name == "separatorLine")
                {
                    panel.BackColor = isDarkMode ? Color.FromArgb(100, 100, 100) : Color.FromArgb(176, 196, 222);
                }
                else if (panel.Name != "filePanel")
                {
                    panel.BackColor = isDarkMode ? Color.FromArgb(42, 58, 84) : Color.FromArgb(240, 248, 255);
                }
                else
                {
                    panel.BackColor = GetPanelColor(control.Tag?.ToString());
                }
            }
            else if (control is FlowLayoutPanel flowPanel)
            {
                flowPanel.BackColor = isDarkMode ? Color.FromArgb(26, 42, 68) : Color.FromArgb(173, 216, 230);
            }
            else
            {
                control.BackColor = isDarkMode ? Color.FromArgb(26, 42, 68) : Color.FromArgb(173, 216, 230);
            }
        }
        #endregion

        #region Change Directory
        private void LoadDefaultDirectory()
        {
            if (System.IO.File.Exists(configFilePath))
            {
                defaultTheme = System.IO.File.ReadAllText(configFilePath).Trim();
                if (Directory.Exists(defaultTheme))
                {
                    defaultDirectory = defaultTheme;
                    return;
                }
                else
                {
                    MessageBox.Show("Saved directory in loadDefaultDirectory.txt does not exist.\nUsing default fallback path.");
                }
            }
            else
            {
                MessageBox.Show("Error loading directory from loadDefaultDirectory.txt:\n");
            }

            defaultDirectory = @"C:\Users\niraj\Downloads";
        }
        private void ChangeDirectory()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "Choose a folder to browse",
                SelectedPath = defaultDirectory
            };

            DialogResult result = dialog.ShowDialog();

            if (result == DialogResult.OK && Directory.Exists(dialog.SelectedPath))
            {
                defaultDirectory = dialog.SelectedPath;
                System.IO.File.WriteAllText(configFilePath, defaultDirectory);
                MessageBox.Show("You selected:\n" + defaultDirectory, "Folder Changed");

                RefreshCurrentTabFiles();
            }
        }
        #endregion

        #region Search And Browse
        private void SearchButton_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Button searchButton)
            {
                TabPage currentTab = tabControl.SelectedTab;
                System.Windows.Forms.TextBox searchTextBox = currentTab?.Controls.Find("searchTextBox", false).FirstOrDefault() as System.Windows.Forms.TextBox;

                if (searchButton.Tag == null)
                    searchButton.Tag = Tuple.Create(searchButton.BackColor, searchButton.ForeColor);

                if (searchTextBox != null && !string.IsNullOrWhiteSpace(searchTextBox.Text) && searchTextBox.Text != "Search Files...")
                {
                    searchButton.BackColor = Color.FromArgb(209, 227, 239);
                    searchButton.ForeColor = Color.Black;
                    searchButton.Cursor = Cursors.Hand;
                }
            }
        }
        private void SearchButton_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Button searchButton && searchButton.Tag is Tuple<Color, Color> originalColors)
            {
                searchButton.BackColor = originalColors.Item1;
                searchButton.ForeColor = originalColors.Item2;
                searchButton.Cursor = Cursors.Default;
                searchButton.Tag = null;
            }
        }
        private void SearchButton_Click(object sender, EventArgs e)
        {
            TabPage currentTab = tabControl.SelectedTab;
            System.Windows.Forms.TextBox searchTextBox = currentTab.Controls.Find("searchTextBox", false).FirstOrDefault() as System.Windows.Forms.TextBox;

            if (searchTextBox.Text.Trim().Length > 0 && searchTextBox.Text != "Search Files...")
            {
                SearchFiles(currentTab, searchTextBox.Text.Trim());
            }
            else
            {
                MessageBox.Show("Please enter a search term.", "Empty Search", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            }
        }
        private void SearchFiles(TabPage tab, string query)
        {
            string[] files = Directory.GetFiles(defaultDirectory, "*.*", SearchOption.AllDirectories);
            string[] matchedFiles = Array.FindAll(files, file => Path.GetFileName(file).ToLower().Contains(query.ToLower()));

            if (matchedFiles.Length == 0)
            {
                MessageBox.Show("No files matched your search.", "No Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var recentFilesPanel = tagDict.ContainsKey("RecentFilesPanel") ? tagDict["RecentFilesPanel"] as FlowLayoutPanel : null;
            var fileDisplayPanel = tagDict.ContainsKey("FileDisplayPanel") ? tagDict["FileDisplayPanel"] as FlowLayoutPanel : null;

            if (recentFilesPanel == null || fileDisplayPanel == null) return;

            recentFilesPanel.Visible = false;
            fileDisplayPanel.Visible = true;
            fileDisplayPanel.Controls.Clear();
            fileDisplayPanel.BringToFront();

            HidePaginationControls(tab);

            foreach (string filePath in matchedFiles)
            {
                string fileType = GetFileTypeFromExtension(Path.GetExtension(filePath).ToLower());
                AddFileDisplay(fileDisplayPanel, filePath, fileType);
            }

            tagDict["CurrentFiles"] = matchedFiles.ToList();

            PositionSearchComponents(tab);
            PositionSeparatorLine(tab);
            ShowSortButton(tab);
        }
        private string GetFileType(string label)
        {
            if (label == "Images") return "Image";
            else if (label == "Video") return "Video";
            else if (label == "Music") return "Music";
            else if (label == "Documents") return "Document";
            else return "";
        }
        private string NormalizeMediaType(string menuType)
        {
            switch (menuType.ToLower())
            {
                case "images": return "Image";
                case "video": return "Video";
                case "music": return "Music";
                case "documents": return "Document";
                default: return "";
            }
        }
        private void OnMediaMenuItemClicked(TabPage tab, string mediaType)
        {
            currentFileType = mediaType;

            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            // Get panels
            var recentFilesPanel = tagDict.ContainsKey("RecentFilesPanel") ? tagDict["RecentFilesPanel"] as FlowLayoutPanel : null;
            var fileDisplayPanel = tagDict.ContainsKey("FileDisplayPanel") ? tagDict["FileDisplayPanel"] as FlowLayoutPanel : null;

            if (recentFilesPanel == null || fileDisplayPanel == null) return;

            // Hide recent files panel and pagination controls
            recentFilesPanel.Visible = false;
            HidePaginationControls(tab);

            // Show and clear file display panel
            fileDisplayPanel.Visible = true;
            fileDisplayPanel.Controls.Clear();
            fileDisplayPanel.BringToFront();

            // Normalize mediaType for filtering
            string normalizedType = NormalizeMediaType(mediaType);

            // Load and display filtered media files
            var mediaFiles = Directory.GetFiles(defaultDirectory, "*.*", SearchOption.AllDirectories)
                .Where(filePath => IsFileTypeMatch(normalizedType, Path.GetExtension(filePath).ToLower()))
                .ToList();

            if (mediaFiles.Count == 0)
            {
                Label noFilesLabel = new Label
                {
                    Text = $"No {mediaType} files found in this directory.",
                    ForeColor = Color.Red,
                    AutoSize = true,
                    Location = new Point(0, 0)
                };
                fileDisplayPanel.Controls.Add(noFilesLabel);
            }
            else
            {
                foreach (string filePath in mediaFiles)
                {
                    AddFileDisplay(fileDisplayPanel, filePath, normalizedType);
                }
            }

            // Update tab state
            tagDict["CurrentFiles"] = mediaFiles;

            // Final layout adjustments
            PositionSearchComponents(tab);
            PositionSeparatorLine(tab);
            ShowSortButton(tab);
            var sortButton = tab.Controls.Find("sortButton", true).FirstOrDefault() as Button;
            if (sortButton != null)
            {
                sortButton.Enabled = true;
            }
        }
        private void SwitchToDefaultFileView()
        {
            currentFileType = "";
            TabPage currentTab = tabControl.SelectedTab;
            if (currentTab == null) return;

            var tagDict = currentTab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var recentFilesPanel = tagDict.ContainsKey("RecentFilesPanel") ? tagDict["RecentFilesPanel"] as FlowLayoutPanel : null;
            var fileDisplayPanel = tagDict.ContainsKey("FileDisplayPanel") ? tagDict["FileDisplayPanel"] as FlowLayoutPanel : null;

            if (recentFilesPanel == null || fileDisplayPanel == null)
            {
                MessageBox.Show("SwitchToDefaultFileView: recentFilesPanel or fileDisplayPanel is null");
                return;
            }

            // Show recent files panel and hide file display panel
            fileDisplayPanel.Visible = false;
            recentFilesPanel.Visible = true;
            recentFilesPanel.BringToFront();

            // Reset pagination and load recent files
            ResetToPagination(currentTab);

            // Update tab state
            tagDict["CurrentFiles"] = new List<string>();

            // Final layout adjustments
            PositionSearchComponents(currentTab);
            PositionSeparatorLine(currentTab);
            PositionPaginationControls(currentTab);
            ShowSortButton(currentTab);
            var sortButton = currentTab.Controls.Find("sortButton", true).FirstOrDefault() as Button;
            if (sortButton != null)
            {
                sortButton.Enabled = false;
            }

        }
        private bool IsFileTypeMatch(string fileType, string extension)
        {
            switch (fileType)
            {
                case "Image":
                    return extension == ".jpg" || extension == ".png" || extension == ".jpeg";
                case "Video":
                    return extension == ".mp4" || extension == ".mkv" ;
                case "Music":
                    return extension == ".mp3" || extension == ".wav";
                case "Document":
                    return extension == ".txt" || extension == ".pdf" || extension == ".docx" || extension == ".pptx" || extension == ".xlsx";
                default:
                    return false;
            }
        }
        private void PositionPaginationControls(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var pageNumberLabel = tagDict["PageNumberLabel"] as Label;
            var previousButton = tagDict["PreviousButton"] as Button;
            var nextButton = tagDict["NextButton"] as Button;
            var recentFilesPanel = tagDict["RecentFilesPanel"] as FlowLayoutPanel;

            if (pageNumberLabel == null || previousButton == null || nextButton == null)
            {
                MessageBox.Show("PositionPaginationControls: One or more pagination controls are null");
                return;
            }

            int tabHeight = tab.ClientSize.Height > 0 ? tab.ClientSize.Height : 480;
            int tabWidth = tab.ClientSize.Width > 0 ? tab.ClientSize.Width : 854;
            int bottomY = recentFilesPanel != null && recentFilesPanel.Visible ? recentFilesPanel.Bottom + 5 : tabHeight - 30;
            int centerX = tabWidth / 2;

            if (bottomY > tabHeight - 30)
            {
                bottomY = tabHeight - 30;
            }

            pageNumberLabel.Location = new Point(centerX - 50, bottomY);
            previousButton.Location = new Point(centerX - 150, bottomY);
            nextButton.Location = new Point(centerX + 50, bottomY);
        }
        private void LoadRecentFilesWithPagination(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null)
            {
                MessageBox.Show("LoadRecentFiles: tagDict is null");
                return;
            }

            var recentFilesPanel = tagDict["RecentFilesPanel"] as FlowLayoutPanel;
            if (recentFilesPanel == null)
            {
                MessageBox.Show("LoadRecentFiles: RecentFilesPanel is null");
                return;
            }

            recentFilesPanel.Controls.Clear();

            string[] allFiles = Directory.GetFiles(defaultDirectory, "*.*", SearchOption.AllDirectories)
                                         .OrderByDescending(f => File.GetLastAccessTimeUtc(f))
                                         .ToArray();

            tagDict["TotalFiles"] = allFiles.Length;

            if (allFiles.Length == 0)
            {
                Label noFilesLabel = new Label
                {
                    Text = "No files found in this directory.",
                    ForeColor = Color.Red,
                    AutoSize = true,
                    Location = new Point(0, 0)
                };
                recentFilesPanel.Controls.Add(noFilesLabel);
                UpdatePaginationControls(tab);
                return;
            }

            int currentPage = (int)tagDict["CurrentPage"];

            var filesForPage = allFiles
                .Skip((currentPage - 1) * filesPerPage)
                .Take(filesPerPage)
                .ToList();

            foreach (string filePath in filesForPage)
            {
                string extension = Path.GetExtension(filePath).ToLower();
                string fileType = GetFileTypeFromExtension(extension);
                AddFileDisplay(recentFilesPanel, filePath, fileType);
            }

            UpdatePaginationControls(tab);
        }
        private void UpdatePaginationControls(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null)
            {
                return;
            }

            var pageNumberLabel = tagDict["PageNumberLabel"] as Label;
            var previousButton = tagDict["PreviousButton"] as Button;
            var nextButton = tagDict["NextButton"] as Button;
            int totalFiles = tagDict.ContainsKey("TotalFiles") ? (int)tagDict["TotalFiles"] : 0;
            int currentPage = tagDict.ContainsKey("CurrentPage") ? (int)tagDict["CurrentPage"] : 1;

            if (pageNumberLabel == null || previousButton == null || nextButton == null)
            {
                MessageBox.Show("UpdatePaginationControls: One or more pagination controls are null");
                return;
            }

            int totalPages = totalFiles > 0 ? (int)Math.Ceiling((double)totalFiles / filesPerPage) : 1;
            pageNumberLabel.Text = $"Page {currentPage} of {totalPages}";
            previousButton.Enabled = currentPage > 1;
            nextButton.Enabled = currentPage < totalPages;

            var recentFilesPanel = tagDict["RecentFilesPanel"] as FlowLayoutPanel;
            bool showControls = recentFilesPanel?.Visible ?? false;
            previousButton.Visible = showControls;
            nextButton.Visible = showControls;
            pageNumberLabel.Visible = showControls;

            ApplyThemeToPaginationControls(tab);
        }
        private string GetFileTypeFromExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                    return "Image";
                case ".mp4":
                case ".mkv":
                    return "Video";
                case ".mp3":
                case ".wav":
                    return "Music";
                case ".txt":
                case ".pdf":
                case ".docx":
                case ".pptx":
                case ".xlsx":
                    return "Document";
                default:
                    return "Document"; // Default fallback
            }
        }
        private void ApplyThemeToPaginationControls(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var pageNumberLabel = tagDict["PageNumberLabel"] as Label;
            var previousButton = tagDict["PreviousButton"] as Button;
            var nextButton = tagDict["NextButton"] as Button;

            if (pageNumberLabel == null || previousButton == null || nextButton == null) return;

            bool isDarkMode = this.BackColor == Color.FromArgb(26, 42, 68);
            Color buttonColor = isDarkMode ? Color.FromArgb(51, 102, 102) : Color.FromArgb(135, 206, 250);
            Color textColor = isDarkMode ? darkTextColor : lightTextColor;

            pageNumberLabel.BackColor = isDarkMode ? Color.FromArgb(26, 42, 68) : Color.FromArgb(173, 216, 230);
            pageNumberLabel.ForeColor = textColor;

            previousButton.BackColor = buttonColor;
            previousButton.ForeColor = Color.Black;
            previousButton.FlatAppearance.BorderColor = isDarkMode ? Color.White : Color.Black;
            previousButton.FlatAppearance.BorderSize = 1;

            nextButton.BackColor = buttonColor;
            nextButton.ForeColor = Color.Black;
            nextButton.FlatAppearance.BorderColor = isDarkMode ? Color.White : Color.Black;
            nextButton.FlatAppearance.BorderSize = 1;
        }
        private void NextButton_Click(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            int currentPage = tagDict.ContainsKey("CurrentPage") ? (int)tagDict["CurrentPage"] : 1;
            int totalFiles = tagDict.ContainsKey("TotalFiles") ? (int)tagDict["TotalFiles"] : 0;
            int totalPages = totalFiles > 0 ? (int)Math.Ceiling((double)totalFiles / filesPerPage) : 1;

            if (currentPage < totalPages)
            {
                tagDict["CurrentPage"] = currentPage + 1;
                LoadRecentFilesWithPagination(tab);
            }
        }
        private void PreviousButton_Click(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            int currentPage = tagDict.ContainsKey("CurrentPage") ? (int)tagDict["CurrentPage"] : 1;
            if (currentPage > 1)
            {
                tagDict["CurrentPage"] = currentPage - 1;
                LoadRecentFilesWithPagination(tab);
            }
        }
        private void HidePaginationControls(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var pageNumberLabel = tagDict.ContainsKey("PageNumberLabel") ? tagDict["PageNumberLabel"] as Label : null;
            var previousButton = tagDict.ContainsKey("PreviousButton") ? tagDict["PreviousButton"] as Button : null;
            var nextButton = tagDict.ContainsKey("NextButton") ? tagDict["NextButton"] as Button : null;

            if (pageNumberLabel != null) pageNumberLabel.Visible = false;
            if (previousButton != null) previousButton.Visible = false;
            if (nextButton != null) nextButton.Visible = false;
        }
        private void ShowPaginationControls(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null)
            {
                MessageBox.Show("ShowPaginationControls: Error: tagDict is null");
                return;
            }

            var pageNumberLabel = tagDict["PageNumberLabel"] as Label;
            var previousButton = tagDict["PreviousButton"] as Button;
            var nextButton = tagDict["NextButton"] as Button;

            if (pageNumberLabel != null)
            {
                pageNumberLabel.Visible = true;
            }

            if (previousButton != null)
            {
                previousButton.Visible = true;
            }

            if (nextButton != null)
            {
                nextButton.Visible = true;
            }

            UpdatePaginationControls(tab);
        }
        private void ResetToPagination(TabPage tab)
        {
            var tagDict = tab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            tagDict["CurrentPage"] = 1;
            ShowPaginationControls(tab);
            LoadRecentFilesWithPagination(tab);
        }
        private void RefreshCurrentTabFiles()
        {
            TabPage currentTab = tabControl.SelectedTab;
            if (currentTab == null) return;

            var tagDict = currentTab.Tag as Dictionary<string, object>;
            if (tagDict == null) return;

            var recentFilesPanel = tagDict.ContainsKey("RecentFilesPanel") ? tagDict["RecentFilesPanel"] as FlowLayoutPanel : null;
            var fileDisplayPanel = tagDict.ContainsKey("FileDisplayPanel") ? tagDict["FileDisplayPanel"] as FlowLayoutPanel : null;

            if (fileDisplayPanel == null || recentFilesPanel == null) return;

            string fileType = GetFileType(currentFileType);
            if (fileType == "") 
            {
                SwitchToDefaultFileView();
                return;
            }

            recentFilesPanel.Visible = false;
            fileDisplayPanel.Visible = true;
            fileDisplayPanel.Controls.Clear();
            fileDisplayPanel.BringToFront();

            HidePaginationControls(currentTab);

            string[] files = Directory.GetFiles(defaultDirectory, "*.*", SearchOption.AllDirectories);
            List<string> currentFiles = files
                .Where(filePath => IsFileTypeMatch(fileType, Path.GetExtension(filePath).ToLower()))
                .ToList();

            foreach (string filePath in currentFiles)
            {
                AddFileDisplay(fileDisplayPanel, filePath, fileType);
            }

            tagDict["CurrentFiles"] = currentFiles;

            PositionSearchComponents(currentTab);
            PositionSeparatorLine(currentTab);
            ShowSortButton(currentTab);
        }
        private void PositionSearchComponents(TabPage tab)
        {
            Control sortButton = tab.Controls.Find("sortButton", false).FirstOrDefault();
            Control searchTextBox = tab.Controls.Find("searchTextBox", false).FirstOrDefault();
            Control searchButton = tab.Controls.Find("searchButton", false).FirstOrDefault();
            Control hamburgerButton = tab.Controls.Find("hamburgerMenuButton", false).FirstOrDefault();

            if (hamburgerButton != null && sortButton != null && searchTextBox != null && searchButton != null)
            {
                int marginRight = 10;
                int marginTop = 5;
                int spacing = 5;

                int hamburgerWidth = hamburgerButton.Width;

                // Position search controls leaving space after hamburger button plus some gap
                int startX = tab.Width - (sortButton.Width + searchTextBox.Width + searchButton.Width + (spacing * 2) + marginRight);

                int startY = marginTop;

                // Make sure startX is not too close to hamburger button
                if (startX < hamburgerWidth + 15)
                    startX = hamburgerWidth + 15;

                sortButton.Location = new Point(startX, startY);
                searchTextBox.Location = new Point(startX + sortButton.Width + spacing, startY);
                searchButton.Location = new Point(searchTextBox.Location.X + searchTextBox.Width + spacing, startY);

                // Optional: vertically align hamburger button with other controls
                int controlsCenterY = startY + sortButton.Height / 2;
                int hamburgerCenterY = hamburgerButton.Location.Y + hamburgerButton.Height / 2;
                int offsetY = controlsCenterY - hamburgerCenterY;
                hamburgerButton.Location = new Point(hamburgerButton.Location.X, hamburgerButton.Location.Y + offsetY);
            }
        }
        #endregion

        #region Helpers

        private void PositionSeparatorLine(TabPage tab)
        {
            Control separatorLine = tab.Controls.Find("separatorLine", false).FirstOrDefault();
            Control hamburgerButton = tab.Controls.Find("hamburgerMenuButton", false).FirstOrDefault();
            Control searchButton = tab.Controls.Find("searchButton", false).FirstOrDefault();

            if (separatorLine != null && hamburgerButton != null && searchButton != null)
            {
                // Find max bottom among hamburger and searchButton (since search textbox & sort button have similar height)
                int maxBottom = Math.Max(hamburgerButton.Bottom, searchButton.Bottom);

                separatorLine.Width = tab.ClientSize.Width;
                separatorLine.Location = new Point(0, maxBottom + 5);
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {

        }
        #endregion

    }
}
