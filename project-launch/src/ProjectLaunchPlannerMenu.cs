using System;
using System.Drawing;
using System.Windows.Forms;

namespace AzerothLauncher
{
    // Experimental menu shell; intentionally does not replace the existing Guide yet.
    // Host must attach the existing TalentTreeView and GearSheet controls.
    public sealed class ProjectLaunchPlannerMenu : UserControl
    {
        public event Action<string> TabChanged;
        public event Action<string> ActionRequested;
        readonly Panel workspace;
        readonly Label heading;
        readonly ComboBox builds;
        readonly Button talentButton;
        readonly Button gearButton;
        string currentTab = "Talents";

        public ProjectLaunchPlannerMenu()
        {
            BackColor = Color.FromArgb(14, 13, 20);
            ForeColor = Color.FromArgb(240, 192, 80);
            heading = new Label { Text = "BUILD PLANNER", Dock = DockStyle.Top,
                Height = 32, TextAlign = ContentAlignment.MiddleCenter };
            Controls.Add(heading);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34,
                WrapContents = false, AutoScroll = false };
            talentButton = MakeButton("TALENTS", delegate { SelectTab("Talents"); });
            gearButton = MakeButton("GEAR", delegate { SelectTab("Gear"); });
            toolbar.Controls.Add(talentButton);
            toolbar.Controls.Add(gearButton);
            Controls.Add(toolbar);

            workspace = new Panel { Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(19, 17, 27) };
            Controls.Add(workspace);
            workspace.BringToFront();

            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70,
                WrapContents = true, AutoScroll = false };
            builds = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            footer.Controls.Add(builds);
            foreach (string action in new[] { "NEW", "SAVE", "RENAME", "IMPORT", "EXPORT" })
            {
                string captured = action;
                footer.Controls.Add(MakeButton(action, delegate {
                    if (ActionRequested != null) ActionRequested(captured);
                }));
            }
            Controls.Add(footer);
            footer.BringToFront();
            toolbar.BringToFront();
            heading.BringToFront();
            SelectTab("Talents");
        }

        static Button MakeButton(string label, Action click)
        {
            var button = new Button { Text = label, AutoSize = true,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(240, 192, 80),
                BackColor = Color.FromArgb(35, 29, 23) };
            button.FlatAppearance.BorderColor = Color.FromArgb(96, 75, 40);
            button.Click += delegate { click(); };
            return button;
        }

        public void SetClass(string className) {
            heading.Text = "BUILD PLANNER  -  " + className;
        }

        public void SetSavedNames(string[] names) {
            builds.Items.Clear();
            if (names != null) builds.Items.AddRange(names);
        }

        public string SelectedBuildName { get { return builds.SelectedItem as string; } }

        public void ShowEditor(Control editor) {
            workspace.Controls.Clear();
            if (editor != null) {
                editor.Dock = DockStyle.Fill;
                workspace.Controls.Add(editor);
            }
        }

        public void SelectTab(string tab) {
            if (tab != "Talents" && tab != "Gear") throw new ArgumentException("Invalid tab.");
            currentTab = tab;
            talentButton.Enabled = tab != "Talents";
            gearButton.Enabled = tab != "Gear";
            if (TabChanged != null) TabChanged(tab);
        }

        public string CurrentTab { get { return currentTab; } }
    }
}
