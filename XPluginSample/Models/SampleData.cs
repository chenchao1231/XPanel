using System;
using System.Collections.Generic;

namespace XPluginSample.Models
{
    /// <summary>
    /// 示例数据模型
    /// </summary>
    public class SampleData
    {
        /// <summary>
        /// 数据ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 数据名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据值
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 验证数据是否有效
        /// </summary>
        /// <returns>true表示数据有效</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Value);
        }

        /// <summary>
        /// 克隆数据对象
        /// </summary>
        /// <returns>克隆的数据对象</returns>
        public SampleData Clone()
        {
            return new SampleData
            {
                Id = this.Id,
                Name = this.Name,
                Value = this.Value,
                CreatedTime = this.CreatedTime,
                IsEnabled = this.IsEnabled,
                Description = this.Description,
                Tags = new List<string>(this.Tags)
            };
        }

        public override string ToString()
        {
            return $"{Name}: {Value} ({(IsEnabled ? "启用" : "禁用")})";
        }
    }

    /// <summary>
    /// 示例配置模型
    /// </summary>
    public class SampleConfig
    {
        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName { get; set; } = "示例配置";

        /// <summary>
        /// 自动保存间隔（秒）
        /// </summary>
        public int AutoSaveInterval { get; set; } = 30;

        /// <summary>
        /// 最大数据条数
        /// </summary>
        public int MaxDataCount { get; set; } = 100;

        /// <summary>
        /// 启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// 主题颜色
        /// </summary>
        public string ThemeColor { get; set; } = "Default";

        /// <summary>
        /// 扩展属性
        /// </summary>
        public Dictionary<string, object> ExtendedProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        /// <returns>true表示配置有效</returns>
        public bool IsValid()
        {
            return AutoSaveInterval > 0 && 
                   MaxDataCount > 0 && 
                   !string.IsNullOrWhiteSpace(ConfigName);
        }
    }
}
