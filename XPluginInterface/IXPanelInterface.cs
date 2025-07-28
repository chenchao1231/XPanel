namespace XPlugin
{
    using System.Windows.Forms;

    /// <summary>
    /// XPanel插件面板接口
    /// </summary>
    public interface IXPanelInterface
    {
        /// <summary>
        /// 插件名称，用于菜单显示
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 创建内容面板
        /// </summary>
        /// <returns>用户控件实例</returns>
        UserControl CreatePanel();
    }
}
