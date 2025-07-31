using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using XPluginSample.Models;

namespace XPluginSample.Utils
{
    /// <summary>
    /// 数据处理辅助工具类
    /// </summary>
    public static class DataHelper
    {
        /// <summary>
        /// 验证数据名称格式
        /// </summary>
        /// <param name="name">数据名称</param>
        /// <returns>true表示格式有效</returns>
        public static bool IsValidDataName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // 名称长度限制
            if (name.Length < 2 || name.Length > 50)
                return false;

            // 不能包含特殊字符
            var invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '\\', '/' };
            return !invalidChars.Any(name.Contains);
        }

        /// <summary>
        /// 清理数据名称
        /// </summary>
        /// <param name="name">原始名称</param>
        /// <returns>清理后的名称</returns>
        public static string CleanDataName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // 移除特殊字符
            var invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '\\', '/' };
            var cleanName = name;
            
            foreach (var invalidChar in invalidChars)
            {
                cleanName = cleanName.Replace(invalidChar.ToString(), "");
            }

            // 移除多余空格
            cleanName = Regex.Replace(cleanName, @"\s+", " ").Trim();

            // 长度限制
            if (cleanName.Length > 50)
            {
                cleanName = cleanName.Substring(0, 50);
            }

            return cleanName;
        }

        /// <summary>
        /// 验证标签格式
        /// </summary>
        /// <param name="tags">标签列表</param>
        /// <returns>true表示所有标签格式有效</returns>
        public static bool IsValidTags(List<string> tags)
        {
            if (tags == null)
                return true;

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    return false;

                if (tag.Length > 20)
                    return false;

                // 标签不能包含逗号和分号
                if (tag.Contains(',') || tag.Contains(';'))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 清理标签列表
        /// </summary>
        /// <param name="tags">原始标签列表</param>
        /// <returns>清理后的标签列表</returns>
        public static List<string> CleanTags(List<string> tags)
        {
            if (tags == null)
                return new List<string>();

            return tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length <= 20)
                .Select(tag => tag.Replace(",", "").Replace(";", ""))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 生成数据摘要
        /// </summary>
        /// <param name="data">数据对象</param>
        /// <returns>数据摘要字符串</returns>
        public static string GenerateDataSummary(SampleData data)
        {
            if (data == null)
                return "无数据";

            var summary = $"名称: {data.Name}";
            
            if (!string.IsNullOrEmpty(data.Value))
            {
                var valuePreview = data.Value.Length > 30 
                    ? data.Value.Substring(0, 30) + "..." 
                    : data.Value;
                summary += $", 值: {valuePreview}";
            }

            summary += $", 状态: {(data.IsEnabled ? "启用" : "禁用")}";
            summary += $", 创建: {data.CreatedTime:yyyy-MM-dd}";

            if (data.Tags.Any())
            {
                summary += $", 标签: {string.Join(", ", data.Tags.Take(3))}";
                if (data.Tags.Count > 3)
                {
                    summary += "...";
                }
            }

            return summary;
        }

        /// <summary>
        /// 导出数据到JSON文件
        /// </summary>
        /// <param name="dataList">数据列表</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>true表示导出成功</returns>
        public static bool ExportToJson(List<SampleData> dataList, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(dataList, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从JSON文件导入数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>数据列表，失败返回null</returns>
        public static List<SampleData>? ImportFromJson(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<SampleData>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 生成数据统计报告
        /// </summary>
        /// <param name="dataList">数据列表</param>
        /// <returns>统计报告字符串</returns>
        public static string GenerateStatisticsReport(List<SampleData> dataList)
        {
            if (dataList == null || !dataList.Any())
                return "无数据统计";

            var report = "=== 数据统计报告 ===\n";
            report += $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";

            // 基本统计
            report += "基本统计:\n";
            report += $"  总数据量: {dataList.Count} 条\n";
            report += $"  启用数据: {dataList.Count(d => d.IsEnabled)} 条\n";
            report += $"  禁用数据: {dataList.Count(d => !d.IsEnabled)} 条\n\n";

            // 时间统计
            if (dataList.Any())
            {
                var oldestData = dataList.OrderBy(d => d.CreatedTime).First();
                var newestData = dataList.OrderByDescending(d => d.CreatedTime).First();

                report += "时间统计:\n";
                report += $"  最早数据: {oldestData.Name} ({oldestData.CreatedTime:yyyy-MM-dd HH:mm})\n";
                report += $"  最新数据: {newestData.Name} ({newestData.CreatedTime:yyyy-MM-dd HH:mm})\n\n";
            }

            // 标签统计
            var allTags = dataList.SelectMany(d => d.Tags).ToList();
            if (allTags.Any())
            {
                var tagGroups = allTags.GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .Take(10);

                report += "热门标签 (前10):\n";
                foreach (var tagGroup in tagGroups)
                {
                    report += $"  {tagGroup.Key}: {tagGroup.Count()} 次\n";
                }
                report += "\n";
            }

            // 数据长度统计
            var valueLengths = dataList.Select(d => d.Value.Length).ToList();
            if (valueLengths.Any())
            {
                report += "数据长度统计:\n";
                report += $"  平均长度: {valueLengths.Average():F1} 字符\n";
                report += $"  最短长度: {valueLengths.Min()} 字符\n";
                report += $"  最长长度: {valueLengths.Max()} 字符\n\n";
            }

            report += "=== 报告结束 ===";
            return report;
        }

        /// <summary>
        /// 搜索数据（支持多种搜索模式）
        /// </summary>
        /// <param name="dataList">数据列表</param>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="searchMode">搜索模式</param>
        /// <returns>匹配的数据列表</returns>
        public static List<SampleData> SearchData(List<SampleData> dataList, string keyword, SearchMode searchMode = SearchMode.Contains)
        {
            if (dataList == null || string.IsNullOrWhiteSpace(keyword))
                return dataList ?? new List<SampleData>();

            var results = new List<SampleData>();

            foreach (var data in dataList)
            {
                bool isMatch = searchMode switch
                {
                    SearchMode.Contains => IsContainsMatch(data, keyword),
                    SearchMode.StartsWith => IsStartsWithMatch(data, keyword),
                    SearchMode.EndsWith => IsEndsWithMatch(data, keyword),
                    SearchMode.Exact => IsExactMatch(data, keyword),
                    SearchMode.Regex => IsRegexMatch(data, keyword),
                    _ => IsContainsMatch(data, keyword)
                };

                if (isMatch)
                {
                    results.Add(data);
                }
            }

            return results;
        }

        private static bool IsContainsMatch(SampleData data, string keyword)
        {
            return data.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Tags.Any(tag => tag.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsStartsWithMatch(SampleData data, string keyword)
        {
            return data.Name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Value.StartsWith(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEndsWithMatch(SampleData data, string keyword)
        {
            return data.Name.EndsWith(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Value.EndsWith(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExactMatch(SampleData data, string keyword)
        {
            return data.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
                   data.Tags.Any(tag => tag.Equals(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRegexMatch(SampleData data, string pattern)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                return regex.IsMatch(data.Name) ||
                       regex.IsMatch(data.Value) ||
                       regex.IsMatch(data.Description) ||
                       data.Tags.Any(tag => regex.IsMatch(tag));
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 搜索模式枚举
    /// </summary>
    public enum SearchMode
    {
        /// <summary>包含</summary>
        Contains,
        /// <summary>开始于</summary>
        StartsWith,
        /// <summary>结束于</summary>
        EndsWith,
        /// <summary>完全匹配</summary>
        Exact,
        /// <summary>正则表达式</summary>
        Regex
    }
}
