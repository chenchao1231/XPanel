using System;
using System.Collections.Generic;
using XPlugin.Auditing;
using XPlugin.Configuration;
using XPlugin.Network;

namespace XPlugin.Services
{
    /// <summary>
    /// 服务工厂接口 - 提供统一的服务创建和管理
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// 创建网络服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="parameters">创建参数</param>
        /// <returns>网络服务实例</returns>
        T CreateNetworkService<T>(params object[] parameters) where T : class, INetworkService;

        /// <summary>
        /// 创建配置服务
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <param name="configPath">配置文件路径</param>
        /// <param name="supportHotReload">是否支持热更新</param>
        /// <returns>配置服务实例</returns>
        IConfigurationService<T> CreateConfigurationService<T>(string configPath, bool supportHotReload = true) where T : class, new();

        /// <summary>
        /// 创建审计服务
        /// </summary>
        /// <param name="options">审计选项</param>
        /// <returns>审计服务实例</returns>
        IAuditService CreateAuditService(AuditServiceOptions options);

        /// <summary>
        /// 注册服务类型
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="lifetime">服务生命周期</param>
        void RegisterService<TInterface, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient)
            where TInterface : class
            where TImplementation : class, TInterface;

        /// <summary>
        /// 注册服务实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="instance">服务实例</param>
        void RegisterInstance<T>(T instance) where T : class;

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例</returns>
        T GetService<T>() where T : class;

        /// <summary>
        /// 尝试获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="service">服务实例</param>
        /// <returns>是否获取成功</returns>
        bool TryGetService<T>(out T? service) where T : class;

        /// <summary>
        /// 获取所有已注册的服务类型
        /// </summary>
        /// <returns>服务类型列表</returns>
        IEnumerable<Type> GetRegisteredServiceTypes();

        /// <summary>
        /// 释放所有服务
        /// </summary>
        void DisposeAll();
    }

    /// <summary>
    /// 服务生命周期
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>瞬态 - 每次请求都创建新实例</summary>
        Transient,
        /// <summary>单例 - 全局唯一实例</summary>
        Singleton,
        /// <summary>作用域 - 在特定作用域内唯一</summary>
        Scoped
    }

    /// <summary>
    /// 审计服务选项
    /// </summary>
    public class AuditServiceOptions
    {
        /// <summary>是否启用</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>审计级别</summary>
        public AuditLevel Level { get; set; } = AuditLevel.Info;

        /// <summary>日志文件路径</summary>
        public string? LogFilePath { get; set; }

        /// <summary>最大文件大小（MB）</summary>
        public int MaxFileSizeMB { get; set; } = 100;

        /// <summary>保留天数</summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>是否启用数据内容记录</summary>
        public bool LogDataContent { get; set; } = true;

        /// <summary>最大数据长度</summary>
        public int MaxDataLength { get; set; } = 1024;

        /// <summary>是否异步写入</summary>
        public bool AsyncWrite { get; set; } = true;

        /// <summary>缓冲区大小</summary>
        public int BufferSize { get; set; } = 1000;
    }

    /// <summary>
    /// 服务描述符
    /// </summary>
    public class ServiceDescriptor
    {
        /// <summary>服务类型</summary>
        public Type ServiceType { get; }

        /// <summary>实现类型</summary>
        public Type? ImplementationType { get; }

        /// <summary>服务实例</summary>
        public object? Instance { get; }

        /// <summary>服务生命周期</summary>
        public ServiceLifetime Lifetime { get; }

        /// <summary>创建时间</summary>
        public DateTime CreatedTime { get; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
            CreatedTime = DateTime.Now;
        }

        public ServiceDescriptor(Type serviceType, object instance)
        {
            ServiceType = serviceType;
            Instance = instance;
            Lifetime = ServiceLifetime.Singleton;
            CreatedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 服务工厂基类 - 提供基本的服务管理功能
    /// </summary>
    public abstract class ServiceFactoryBase : IServiceFactory, IDisposable
    {
        protected readonly Dictionary<Type, ServiceDescriptor> _services = new();
        protected readonly Dictionary<Type, object> _singletonInstances = new();
        protected bool _disposed = false;

        public abstract T CreateNetworkService<T>(params object[] parameters) where T : class, INetworkService;
        public abstract IConfigurationService<T> CreateConfigurationService<T>(string configPath, bool supportHotReload = true) where T : class, new();
        public abstract IAuditService CreateAuditService(AuditServiceOptions options);

        public virtual void RegisterService<TInterface, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            var descriptor = new ServiceDescriptor(typeof(TInterface), typeof(TImplementation), lifetime);
            _services[typeof(TInterface)] = descriptor;
        }

        public virtual void RegisterInstance<T>(T instance) where T : class
        {
            var descriptor = new ServiceDescriptor(typeof(T), instance);
            _services[typeof(T)] = descriptor;
            _singletonInstances[typeof(T)] = instance;
        }

        public virtual T GetService<T>() where T : class
        {
            if (TryGetService<T>(out var service))
                return service!;

            throw new InvalidOperationException($"服务类型 {typeof(T).Name} 未注册");
        }

        public virtual bool TryGetService<T>(out T? service) where T : class
        {
            service = null;

            if (!_services.TryGetValue(typeof(T), out var descriptor))
                return false;

            try
            {
                service = CreateServiceInstance<T>(descriptor);
                return service != null;
            }
            catch
            {
                return false;
            }
        }

        public virtual IEnumerable<Type> GetRegisteredServiceTypes()
        {
            return _services.Keys;
        }

        public virtual void DisposeAll()
        {
            foreach (var instance in _singletonInstances.Values)
            {
                if (instance is IDisposable disposable)
                    disposable.Dispose();
            }

            _singletonInstances.Clear();
            _services.Clear();
        }

        protected virtual T? CreateServiceInstance<T>(ServiceDescriptor descriptor) where T : class
        {
            // 如果是单例且已创建，直接返回
            if (descriptor.Lifetime == ServiceLifetime.Singleton && _singletonInstances.TryGetValue(typeof(T), out var singletonInstance))
            {
                return (T)singletonInstance;
            }

            // 如果有实例，直接返回
            if (descriptor.Instance != null)
            {
                return (T)descriptor.Instance;
            }

            // 创建新实例
            if (descriptor.ImplementationType != null)
            {
                var instance = Activator.CreateInstance(descriptor.ImplementationType);
                if (instance is T typedInstance)
                {
                    // 如果是单例，缓存实例
                    if (descriptor.Lifetime == ServiceLifetime.Singleton)
                    {
                        _singletonInstances[typeof(T)] = typedInstance;
                    }

                    return typedInstance;
                }
            }

            return null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                DisposeAll();
                _disposed = true;
            }
        }
    }
}
