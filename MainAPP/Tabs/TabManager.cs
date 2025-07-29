using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XPanel.Tabs
{
    public class TabManager
    {
        private readonly TabControl tabControl;
        private readonly ToolStripStatusLabel statusLabel;

        public TabManager(TabControl tabControl, ToolStripStatusLabel statusLabel)
        {
            this.tabControl = tabControl;
            this.statusLabel = statusLabel;

            this.tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;


            this.tabControl.SizeMode = TabSizeMode.Fixed;
            this.tabControl.ItemSize = new Size(90, 30); // 120宽度，30高度，调成合适值

            this.tabControl.Padding = new Point(0,0);
            this.tabControl.DrawItem += DrawTabItem;
            this.tabControl.MouseDown += HandleMouseDown;
        }

        public void AddTab(string title, UserControl control)
        {
            foreach (TabPage page in tabControl.TabPages)
            {
                if (page.Text == title)
                {
                    tabControl.SelectedTab = page;
                    statusLabel.Text = $"切换到：{title}";
                    return;
                }
            }

            var tabPage = new TabPage(title)
            {
                Name = "tab_" + title.Replace(" ", "_")
            };
            control.Dock = DockStyle.Fill;
            tabPage.BorderStyle = BorderStyle.FixedSingle;
            tabPage.Controls.Add(control);
            tabControl.TabPages.Add(tabPage);
            tabControl.SelectedTab = tabPage;

            statusLabel.Text = $"已打开：{title}";
        }

        private void DrawTabItem(object sender, DrawItemEventArgs e)
        {
            var tabPage = tabControl.TabPages[e.Index];
            var rect = e.Bounds;

            // 设置选中页签背景色
            if (e.Index == tabControl.SelectedIndex)
            {
                using (SolidBrush brush = new SolidBrush(Color.LightBlue))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(Color.LightGray))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            }



            Size textSize = TextRenderer.MeasureText(tabPage.Text, e.Font);
            TextRenderer.DrawText(e.Graphics, tabPage.Text, e.Font, rect, tabPage.ForeColor);
            var closeRect = new Rectangle(rect.Left + textSize.Width + 6, rect.Top + 4, 12, 12);
            e.Graphics.DrawString("×", SystemFonts.DefaultFont, Brushes.DarkRed, closeRect);
        }

        private void HandleMouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var tabRect = tabControl.GetTabRect(i);
                var closeRect = new Rectangle(tabRect.Right - 18, tabRect.Top + 4, 12, 12);

                if (closeRect.Contains(e.Location))
                {
                    var tab = tabControl.TabPages[i];
                    tabControl.TabPages.Remove(tab);
                    statusLabel.Text = $"已关闭：{tab.Text}";
                    return;
                }
            }
        }
    }
}
