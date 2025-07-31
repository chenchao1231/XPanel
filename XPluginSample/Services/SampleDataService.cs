using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using XPluginSample.Models;
using XPlugin.logs;

namespace XPluginSample.Services
{
    /// <summary>
    /// 示例数据服务
    /// </summary>
    public class SampleDataService : IDisposable
    {
        private readonly List<SampleData> _dataList;
        private readonly SampleConfig _config;
        private readonly string _dataFilePath;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        /// <summary>
        /// 数据变更事件
        /// </summary>
        public event EventHandler<DataChangedEventArgs>? DataChanged;

        public SampleDataService()
        {
            _dataList = new List<SampleData>();
            _config = new SampleConfig();
            _dataFilePath = Path.Combine("Config", "sample_data.json");
            
            // 确保配置目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
            
            // 加载数据
            LoadData();
            
            Log.Info("示例数据服务已启动");
        }

        /// <summary>
        /// 获取所有数据
        /// </summary>
        /// <returns>数据列表</returns>
        public List<SampleData> GetAllData()
        {
            lock (_lockObject)
            {
                return _dataList.Select(d => d.Clone()).ToList();
            }
        }

        /// <summary>
        /// 根据ID获取数据
        /// </summary>
        /// <param name="id">数据ID</param>
        /// <returns>数据对象，不存在返回null</returns>
        public SampleData? GetDataById(string id)
        {
            lock (_lockObject)
            {
                return _dataList.FirstOrDefault(d => d.Id == id)?.Clone();
            }
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="data">要添加的数据</param>
        /// <returns>true表示添加成功</returns>
        public bool AddData(SampleData data)
        {
            if (data == null || !data.IsValid())
            {
                Log.Warn("尝试添加无效数据");
                return false;
            }

            lock (_lockObject)
            {
                // 检查是否超过最大数量限制
                if (_dataList.Count >= _config.MaxDataCount)
                {
                    Log.Warn($"数据数量已达到最大限制: {_config.MaxDataCount}");
                    return false;
                }

                // 检查名称是否重复
                if (_dataList.Any(d => d.Name == data.Name))
                {
                    Log.Warn($"数据名称已存在: {data.Name}");
                    return false;
                }

                _dataList.Add(data.Clone());
                Log.Info($"添加数据: {data.Name}");
                
                // 触发数据变更事件
                OnDataChanged(new DataChangedEventArgs(DataChangeType.Added, data));
                
                // 自动保存
                _ = Task.Run(SaveDataAsync);
                
                return true;
            }
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="data">要更新的数据</param>
        /// <returns>true表示更新成功</returns>
        public bool UpdateData(SampleData data)
        {
            if (data == null || !data.IsValid())
            {
                Log.Warn("尝试更新无效数据");
                return false;
            }

            lock (_lockObject)
            {
                var existingData = _dataList.FirstOrDefault(d => d.Id == data.Id);
                if (existingData == null)
                {
                    Log.Warn($"要更新的数据不存在: {data.Id}");
                    return false;
                }

                // 检查名称是否与其他数据重复
                if (_dataList.Any(d => d.Id != data.Id && d.Name == data.Name))
                {
                    Log.Warn($"数据名称已存在: {data.Name}");
                    return false;
                }

                // 更新数据
                var index = _dataList.IndexOf(existingData);
                _dataList[index] = data.Clone();
                
                Log.Info($"更新数据: {data.Name}");
                
                // 触发数据变更事件
                OnDataChanged(new DataChangedEventArgs(DataChangeType.Updated, data));
                
                // 自动保存
                _ = Task.Run(SaveDataAsync);
                
                return true;
            }
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="id">数据ID</param>
        /// <returns>true表示删除成功</returns>
        public bool DeleteData(string id)
        {
            lock (_lockObject)
            {
                var data = _dataList.FirstOrDefault(d => d.Id == id);
                if (data == null)
                {
                    Log.Warn($"要删除的数据不存在: {id}");
                    return false;
                }

                _dataList.Remove(data);
                Log.Info($"删除数据: {data.Name}");
                
                // 触发数据变更事件
                OnDataChanged(new DataChangedEventArgs(DataChangeType.Deleted, data));
                
                // 自动保存
                _ = Task.Run(SaveDataAsync);
                
                return true;
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearAllData()
        {
            lock (_lockObject)
            {
                var count = _dataList.Count;
                _dataList.Clear();
                Log.Info($"清空所有数据，共删除 {count} 条");
                
                // 触发数据变更事件
                OnDataChanged(new DataChangedEventArgs(DataChangeType.Cleared, null));
                
                // 自动保存
                _ = Task.Run(SaveDataAsync);
            }
        }

        /// <summary>
        /// 搜索数据
        /// </summary>
        /// <param name="keyword">关键词</param>
        /// <returns>匹配的数据列表</returns>
        public List<SampleData> SearchData(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return GetAllData();
            }

            lock (_lockObject)
            {
                return _dataList
                    .Where(d => d.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                               d.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                               d.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.Clone())
                    .ToList();
            }
        }

        /// <summary>
        /// 获取数据统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public DataStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new DataStatistics
                {
                    TotalCount = _dataList.Count,
                    EnabledCount = _dataList.Count(d => d.IsEnabled),
                    DisabledCount = _dataList.Count(d => !d.IsEnabled),
                    OldestData = _dataList.OrderBy(d => d.CreatedTime).FirstOrDefault(),
                    NewestData = _dataList.OrderByDescending(d => d.CreatedTime).FirstOrDefault()
                };
            }
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    var data = JsonSerializer.Deserialize<List<SampleData>>(json);
                    
                    if (data != null)
                    {
                        lock (_lockObject)
                        {
                            _dataList.Clear();
                            _dataList.AddRange(data);
                        }
                        Log.Info($"加载数据成功，共 {data.Count} 条");
                    }
                }
                else
                {
                    // 创建示例数据
                    CreateSampleData();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载数据失败: {ex.Message}");
                CreateSampleData();
            }
        }

        /// <summary>
        /// 异步保存数据
        /// </summary>
        private async Task SaveDataAsync()
        {
            try
            {
                List<SampleData> dataToSave;
                lock (_lockObject)
                {
                    dataToSave = _dataList.Select(d => d.Clone()).ToList();
                }

                var json = JsonSerializer.Serialize(dataToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_dataFilePath, json);
                Log.Info($"保存数据成功，共 {dataToSave.Count} 条");
            }
            catch (Exception ex)
            {
                Log.Error($"保存数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建示例数据
        /// </summary>
        private void CreateSampleData()
        {
            var sampleData = new List<SampleData>
            {
                new SampleData
                {
                    Name = "示例数据1",
                    Value = "这是第一个示例数据",
                    Description = "用于演示插件功能的示例数据",
                    Tags = new List<string> { "示例", "测试" }
                },
                new SampleData
                {
                    Name = "示例数据2",
                    Value = "这是第二个示例数据",
                    Description = "另一个用于演示的数据",
                    IsEnabled = false,
                    Tags = new List<string> { "示例", "禁用" }
                }
            };

            lock (_lockObject)
            {
                _dataList.AddRange(sampleData);
            }

            _ = Task.Run(SaveDataAsync);
            Log.Info("创建示例数据完成");
        }

        /// <summary>
        /// 触发数据变更事件
        /// </summary>
        private void OnDataChanged(DataChangedEventArgs args)
        {
            DataChanged?.Invoke(this, args);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // 保存数据
                _ = Task.Run(SaveDataAsync).Wait(5000);
                
                Log.Info("示例数据服务已停止");
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 数据变更事件参数
    /// </summary>
    public class DataChangedEventArgs : EventArgs
    {
        public DataChangeType ChangeType { get; }
        public SampleData? Data { get; }

        public DataChangedEventArgs(DataChangeType changeType, SampleData? data)
        {
            ChangeType = changeType;
            Data = data;
        }
    }

    /// <summary>
    /// 数据变更类型
    /// </summary>
    public enum DataChangeType
    {
        Added,
        Updated,
        Deleted,
        Cleared
    }

    /// <summary>
    /// 数据统计信息
    /// </summary>
    public class DataStatistics
    {
        public int TotalCount { get; set; }
        public int EnabledCount { get; set; }
        public int DisabledCount { get; set; }
        public SampleData? OldestData { get; set; }
        public SampleData? NewestData { get; set; }
    }
}
