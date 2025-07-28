using System;

namespace XPlugin.logs
{
    public class UILogOutput : ILogOutput
    {
        private readonly Action<string> _appendToUI;

        public UILogOutput(Action<string> appendAction)
        {
            _appendToUI = appendAction;
        }

        public void AppendLog(string message)
        {
            _appendToUI?.Invoke(message);
        }
    }
}
