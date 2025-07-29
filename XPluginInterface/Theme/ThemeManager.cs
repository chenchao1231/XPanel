using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace XPlugin.Theme
{
    /// <summary>
    /// 主题管理器 - 简化版本
    /// </summary>
    public static class ThemeManager
    {
        private static ThemeConfig _currentTheme = new ThemeConfig();
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "theme.json");

        /// <summary>
        /// 主题变化事件
        /// </summary>
        public static event Action<ThemeConfig>? ThemeChanged;

        /// <summary>
        /// 初始化主题管理器
        /// </summary>
        public static void Initialize()
        {
            LoadTheme();
        }

        /// <summary>
        /// 获取当前主题
        /// </summary>
        public static ThemeConfig CurrentTheme => _currentTheme;

        /// <summary>
        /// 设置主题
        /// </summary>
        public static void SetTheme(ThemeConfig theme)
        {
            _currentTheme = theme;
            SaveTheme();
            ThemeChanged?.Invoke(_currentTheme);
        }

        /// <summary>
        /// 应用主题到控件 - 简化版本
        /// </summary>
        public static void ApplyTheme(Control control)
        {
            if (control == null) return;

            try
            {
                // 应用背景色和前景色
                control.BackColor = _currentTheme.BackgroundColor;
                control.ForeColor = _currentTheme.ForegroundColor;

                // 递归应用到子控件
                foreach (Control child in control.Controls)
                {
                    ApplyTheme(child);
                }
            }
            catch (Exception ex)
            {
                // 忽略主题应用错误
                System.Diagnostics.Debug.WriteLine($"应用主题失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取预定义主题
        /// </summary>
        public static ThemeConfig[] GetPredefinedThemes()
        {
            return new[]
            {
                new ThemeConfig
                {
                    Name = "默认浅色",
                    BackgroundColor = Color.White,
                    ForegroundColor = Color.Black,
                    ListBackgroundColor = Color.White,
                    InputBackgroundColor = Color.White,
                    LogBackgroundColor = Color.Black,
                    LogForegroundColor = Color.LightGreen
                },
                new ThemeConfig
                {
                    Name = "深色主题",
                    BackgroundColor = Color.FromArgb(45, 45, 48),
                    ForegroundColor = Color.White,
                    ListBackgroundColor = Color.FromArgb(37, 37, 38),
                    InputBackgroundColor = Color.FromArgb(51, 51, 55),
                    LogBackgroundColor = Color.FromArgb(30, 30, 30),
                    LogForegroundColor = Color.LightGreen
                },
                new ThemeConfig
                {
                    Name = "蓝色主题",
                    BackgroundColor = Color.FromArgb(240, 248, 255),
                    ForegroundColor = Color.DarkBlue,
                    ListBackgroundColor = Color.FromArgb(230, 240, 250),
                    InputBackgroundColor = Color.White,
                    LogBackgroundColor = Color.FromArgb(25, 25, 112),
                    LogForegroundColor = Color.Cyan
                }
            };
        }

        /// <summary>
        /// 加载主题配置
        /// </summary>
        private static void LoadTheme()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var theme = JsonConvert.DeserializeObject<ThemeConfig>(json);
                    if (theme != null)
                    {
                        _currentTheme = theme;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载主题配置失败: {ex.Message}");
                _currentTheme = new ThemeConfig(); // 使用默认主题
            }
        }

        /// <summary>
        /// 保存主题配置
        /// </summary>
        private static void SaveTheme()
        {
            try
            {
                var configDir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir!);
                }

                var json = JsonConvert.SerializeObject(_currentTheme, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存主题配置失败: {ex.Message}");
            }
        }
    }
}
