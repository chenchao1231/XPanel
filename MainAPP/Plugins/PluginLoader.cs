using XPlugin;
using XPlugin.logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace XPanel.Plugins
{
    public class PluginLoader
    {
        public static List<IServerPlugin> LoadPlugins(string pluginFolder)
        {
            var plugins = new List<IServerPlugin>();
            if (!Directory.Exists(pluginFolder)) return plugins;

            foreach (var file in Directory.GetFiles(pluginFolder, "*.dll"))
            {
                try
                {
                    var asm = Assembly.LoadFrom(file);
                    var types = asm.GetTypes().Where(t =>
                        typeof(IServerPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

                    foreach (var type in types)
                    {
                        var instance = (IServerPlugin)Activator.CreateInstance(type)!;
                        plugins.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"加载插件失败: {file}, 原因: {ex.Message}");
                }
            }

            return plugins;
        }
    }
}
