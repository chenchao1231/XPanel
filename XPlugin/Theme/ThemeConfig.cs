using System;
using System.Drawing;
using Newtonsoft.Json;

namespace XPlugin.Theme
{
    /// <summary>
    /// 主题配置
    /// </summary>
    public class ThemeConfig
    {
        /// <summary>
        /// 主题名称
        /// </summary>
        public string Name { get; set; } = "默认主题";

        /// <summary>
        /// 背景颜色
        /// </summary>
        [JsonIgnore]
        public Color BackgroundColor { get; set; } = Color.White;

        /// <summary>
        /// 前景颜色（文字颜色）
        /// </summary>
        [JsonIgnore]
        public Color ForegroundColor { get; set; } = Color.Black;

        /// <summary>
        /// 列表背景颜色
        /// </summary>
        [JsonIgnore]
        public Color ListBackgroundColor { get; set; } = Color.White;

        /// <summary>
        /// 输入框背景颜色
        /// </summary>
        [JsonIgnore]
        public Color InputBackgroundColor { get; set; } = Color.White;

        /// <summary>
        /// 日志背景颜色
        /// </summary>
        [JsonIgnore]
        public Color LogBackgroundColor { get; set; } = Color.Black;

        /// <summary>
        /// 日志前景颜色
        /// </summary>
        [JsonIgnore]
        public Color LogForegroundColor { get; set; } = Color.LightGreen;

        // JSON序列化用的颜色属性（使用ARGB值）
        [JsonProperty("BackgroundColor")]
        public int BackgroundColorArgb
        {
            get => BackgroundColor.ToArgb();
            set => BackgroundColor = Color.FromArgb(value);
        }

        [JsonProperty("ForegroundColor")]
        public int ForegroundColorArgb
        {
            get => ForegroundColor.ToArgb();
            set => ForegroundColor = Color.FromArgb(value);
        }

        [JsonProperty("ListBackgroundColor")]
        public int ListBackgroundColorArgb
        {
            get => ListBackgroundColor.ToArgb();
            set => ListBackgroundColor = Color.FromArgb(value);
        }

        [JsonProperty("InputBackgroundColor")]
        public int InputBackgroundColorArgb
        {
            get => InputBackgroundColor.ToArgb();
            set => InputBackgroundColor = Color.FromArgb(value);
        }

        [JsonProperty("LogBackgroundColor")]
        public int LogBackgroundColorArgb
        {
            get => LogBackgroundColor.ToArgb();
            set => LogBackgroundColor = Color.FromArgb(value);
        }

        [JsonProperty("LogForegroundColor")]
        public int LogForegroundColorArgb
        {
            get => LogForegroundColor.ToArgb();
            set => LogForegroundColor = Color.FromArgb(value);
        }

        /// <summary>
        /// 创建主题副本
        /// </summary>
        public ThemeConfig Clone()
        {
            return new ThemeConfig
            {
                Name = Name,
                BackgroundColor = BackgroundColor,
                ForegroundColor = ForegroundColor,
                ListBackgroundColor = ListBackgroundColor,
                InputBackgroundColor = InputBackgroundColor,
                LogBackgroundColor = LogBackgroundColor,
                LogForegroundColor = LogForegroundColor
            };
        }

        /// <summary>
        /// 判断是否为深色主题
        /// </summary>
        public bool IsDarkTheme()
        {
            return BackgroundColor.GetBrightness() < 0.5;
        }

        /// <summary>
        /// 获取对比色
        /// </summary>
        public Color GetContrastColor(Color baseColor)
        {
            var brightness = baseColor.GetBrightness();
            return brightness > 0.5 ? Color.Black : Color.White;
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object? obj)
        {
            if (obj is ThemeConfig other)
            {
                return BackgroundColor.Equals(other.BackgroundColor) &&
                       ForegroundColor.Equals(other.ForegroundColor) &&
                       ListBackgroundColor.Equals(other.ListBackgroundColor) &&
                       InputBackgroundColor.Equals(other.InputBackgroundColor) &&
                       LogBackgroundColor.Equals(other.LogBackgroundColor) &&
                       LogForegroundColor.Equals(other.LogForegroundColor);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                BackgroundColor,
                ForegroundColor,
                ListBackgroundColor,
                InputBackgroundColor,
                LogBackgroundColor,
                LogForegroundColor
            );
        }
    }
}
