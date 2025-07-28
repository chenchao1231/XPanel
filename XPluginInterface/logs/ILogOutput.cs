namespace XPlugin.logs
{
    /// <summary>
    /// 日志输出接口
    /// </summary>
    public interface ILogOutput
    {
        void AppendLog(string log);
    }
}
