using XPlugin.logs;
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
    public partial class AboutPanel : UserControl
    {
        public AboutPanel()
        {
            var label = new Label
            {
                Text = "XPanel - 插件化管理面板\n版本: 1.0.0\n基于 .NET 8.0 开发",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            Controls.Add(label);
        }
    }
}
