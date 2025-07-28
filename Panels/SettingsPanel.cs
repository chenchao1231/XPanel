using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XPanel.Panels
{
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            var label = new Label
            {
                Text = "这里是系统设置",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            Controls.Add(label);
        }
    }
}
