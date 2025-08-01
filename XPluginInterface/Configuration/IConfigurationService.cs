using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace XPlugin.Configuration
{
    /// <summary>
    /// 配置服务接口 - 定义配置管理的标准规范
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    public interface IConfigurationService<T> : IDisposable where T : class, new()
    {
        /// <summary>
        /// 配置变更事件
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs<T>>? ConfigurationChanged;

        /// <summary>
        /// 当前配置
        /// </summary>
        T Configuration { get; }

        /// <summary>
        /// 配置文件路径
        /// </summary>
        string ConfigurationPath { get; }

        /// <summary>
        /// 是否支持热更新
        /// </summary>
        bool SupportsHotReload { get; }

        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns>加载的配置对象</returns>
        Task<T> LoadAsync();

        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="configuration">要保存的配置</param>
        /// <returns>保存是否成功</returns>
        Task<bool> SaveAsync(T configuration);

        /// <summary>
        /// 重新加载配置
        /// </summary>
        /// <returns>重新加载是否成功</returns>
        Task<bool> ReloadAsync();

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        /// <param name="configuration">要验证的配置</param>
        /// <returns>验证结果</returns>
        ConfigurationValidationResult Validate(T configuration);

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置对象</returns>
        T CreateDefault();

        /// <summary>
        /// 备份当前配置
        /// </summary>
        /// <returns>备份是否成功</returns>
        Task<bool> BackupAsync();

        /// <summary>
        /// 恢复配置
        /// </summary>
        /// <param name="backupPath">备份文件路径</param>
        /// <returns>恢复是否成功</returns>
        Task<bool> RestoreAsync(string backupPath);
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    public class ConfigurationChangedEventArgs<T> : EventArgs where T : class
    {
        /// <summary>旧配置</summary>
        public T? OldConfiguration { get; }

        /// <summary>新配置</summary>
        public T NewConfiguration { get; }

        /// <summary>变更类型</summary>
        public ConfigurationChangeType ChangeType { get; }

        /// <summary>变更时间</summary>
        public DateTime ChangeTime { get; }

        public ConfigurationChangedEventArgs(T? oldConfiguration, T newConfiguration, ConfigurationChangeType changeType)
        {
            OldConfiguration = oldConfiguration;
            NewConfiguration = newConfiguration;
            ChangeType = changeType;
            ChangeTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 配置变更类型
    /// </summary>
    public enum ConfigurationChangeType
    {
        /// <summary>加载</summary>
        Loaded,
        /// <summary>保存</summary>
        Saved,
        /// <summary>重新加载</summary>
        Reloaded,
        /// <summary>恢复</summary>
        Restored
    }

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>是否有效</summary>
        public bool IsValid { get; }

        /// <summary>错误消息列表</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>警告消息列表</summary>
        public IReadOnlyList<string> Warnings { get; }

        public ConfigurationValidationResult(bool isValid, IEnumerable<string>? errors = null, IEnumerable<string>? warnings = null)
        {
            IsValid = isValid;
            Errors = errors?.ToList() ?? new List<string>();
            Warnings = warnings?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// 创建成功的验证结果
        /// </summary>
        public static ConfigurationValidationResult Success(IEnumerable<string>? warnings = null)
        {
            return new ConfigurationValidationResult(true, null, warnings);
        }

        /// <summary>
        /// 创建失败的验证结果
        /// </summary>
        public static ConfigurationValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
        {
            return new ConfigurationValidationResult(false, errors, warnings);
        }

        /// <summary>
        /// 创建失败的验证结果
        /// </summary>
        public static ConfigurationValidationResult Failure(string error, IEnumerable<string>? warnings = null)
        {
            return new ConfigurationValidationResult(false, new[] { error }, warnings);
        }
    }

    /// <summary>
    /// 可验证的配置接口
    /// </summary>
    public interface IValidatableConfiguration
    {
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        /// <returns>验证结果</returns>
        ConfigurationValidationResult Validate();
    }

    /// <summary>
    /// 可克隆的配置接口
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    public interface ICloneableConfiguration<T> where T : class
    {
        /// <summary>
        /// 克隆配置对象
        /// </summary>
        /// <returns>克隆的配置对象</returns>
        T Clone();
    }

    /// <summary>
    /// 配置基类 - 提供通用的配置功能
    /// </summary>
    public abstract class ConfigurationBase : IValidatableConfiguration, ICloneableConfiguration<ConfigurationBase>
    {
        /// <summary>配置版本</summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>创建时间</summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>最后修改时间</summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>配置描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public virtual ConfigurationValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // 基本验证
            if (string.IsNullOrWhiteSpace(Version))
                errors.Add("配置版本不能为空");

            if (CreatedTime == default)
                warnings.Add("创建时间未设置");

            if (LastModified == default)
                warnings.Add("最后修改时间未设置");

            // 调用子类的验证逻辑
            ValidateCore(errors, warnings);

            return errors.Count > 0 
                ? ConfigurationValidationResult.Failure(errors, warnings)
                : ConfigurationValidationResult.Success(warnings);
        }

        /// <summary>
        /// 子类实现的核心验证逻辑
        /// </summary>
        /// <param name="errors">错误列表</param>
        /// <param name="warnings">警告列表</param>
        protected virtual void ValidateCore(List<string> errors, List<string> warnings)
        {
            // 子类重写此方法实现具体的验证逻辑
        }

        /// <summary>
        /// 克隆配置对象
        /// </summary>
        public virtual ConfigurationBase Clone()
        {
            // 使用序列化方式进行深拷贝
            var json = System.Text.Json.JsonSerializer.Serialize(this, GetType());
            return (ConfigurationBase)System.Text.Json.JsonSerializer.Deserialize(json, GetType())!;
        }

        /// <summary>
        /// 更新最后修改时间
        /// </summary>
        protected void UpdateLastModified()
        {
            LastModified = DateTime.Now;
        }
    }
}
