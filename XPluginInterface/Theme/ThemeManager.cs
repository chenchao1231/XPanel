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
        /// 应用主题到控件
        /// </summary>
        public static void ApplyTheme(Control control)
        {
            if (control == null) return;

            try
            {
                ApplyThemeToControl(control);

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
        /// 应用主题到特定控件
        /// </summary>
        private static void ApplyThemeToControl(Control control)
        {
            try
            {
                switch (control)
                {
                    case Form form:
                        form.BackColor = _currentTheme.BackgroundColor;
                        form.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case Panel panel:
                        panel.BackColor = _currentTheme.BackgroundColor;
                        panel.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case GroupBox groupBox:
                        groupBox.BackColor = _currentTheme.BackgroundColor;
                        groupBox.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case ListView listView:
                        listView.BackColor = _currentTheme.ListBackgroundColor;
                        listView.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case TextBox textBox:
                        if (textBox.ReadOnly)
                        {
                            // 日志文本框保持特殊样式
                            if (textBox.BackColor == Color.Black || textBox.Font.Name == "Consolas")
                            {
                                textBox.BackColor = _currentTheme.LogBackgroundColor;
                                textBox.ForeColor = _currentTheme.LogForegroundColor;
                            }
                            else
                            {
                                textBox.BackColor = _currentTheme.ListBackgroundColor;
                                textBox.ForeColor = _currentTheme.ForegroundColor;
                            }
                        }
                        else
                        {
                            textBox.BackColor = _currentTheme.InputBackgroundColor;
                            textBox.ForeColor = _currentTheme.ForegroundColor;
                        }
                        break;

                    case Button button:
                        // 保持按钮的功能颜色，但调整基础色调
                        if (button.UseVisualStyleBackColor)
                        {
                            button.BackColor = _currentTheme.BackgroundColor;
                        }
                        else
                        {
                            // 保持按钮的特殊颜色，只调整亮度
                            button.BackColor = AdjustButtonColor(button.BackColor);
                        }
                        button.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case Label label:
                        // 保持特殊标签的颜色（如标题、状态标签）
                        if (label.ForeColor != Color.DarkBlue &&
                            label.ForeColor != Color.DarkGreen &&
                            label.ForeColor != Color.Red &&
                            label.Font.Style != FontStyle.Bold)
                        {
                            label.ForeColor = _currentTheme.ForegroundColor;
                        }
                        // 标签背景通常透明，不需要设置
                        break;

                    case ComboBox comboBox:
                        comboBox.BackColor = _currentTheme.InputBackgroundColor;
                        comboBox.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case NumericUpDown numericUpDown:
                        numericUpDown.BackColor = _currentTheme.InputBackgroundColor;
                        numericUpDown.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    case CheckBox checkBox:
                        checkBox.BackColor = _currentTheme.BackgroundColor;
                        checkBox.ForeColor = _currentTheme.ForegroundColor;
                        break;

                    default:
                        // 对于其他控件，应用基本主题
                        if (control.BackColor != Color.Transparent)
                        {
                            control.BackColor = _currentTheme.BackgroundColor;
                        }
                        control.ForeColor = _currentTheme.ForegroundColor;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用控件主题失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调整按钮颜色以适应主题
        /// </summary>
        private static Color AdjustButtonColor(Color originalColor)
        {
            // 根据当前主题调整按钮颜色的亮度
            var brightness = _currentTheme.BackgroundColor.GetBrightness();

            if (brightness < 0.5) // 深色主题
            {
                return Color.FromArgb(
                    Math.Min(255, originalColor.R + 40),
                    Math.Min(255, originalColor.G + 40),
                    Math.Min(255, originalColor.B + 40)
                );
            }
            else // 浅色主题
            {
                return originalColor;
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
