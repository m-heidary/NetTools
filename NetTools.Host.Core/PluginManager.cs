using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NetTools.PluginContracts;

namespace NetTools.Host.Core
{
    public class PluginManager
    {
        public IReadOnlyList<IConsoleNetworkToolPlugin> LoadConsolePlugins(
            string pluginsRoot,
            IPluginHostContext context)
        {
            var result = new List<IConsoleNetworkToolPlugin>();

            if (!Directory.Exists(pluginsRoot))
            {
                return result;
            }

            var pluginDirectories = Directory.GetDirectories(pluginsRoot);

            foreach (var dir in pluginDirectories)
            {
                foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                {
                    Assembly asm;
                    try
                    {
                        asm = Assembly.LoadFrom(dll);
                    }
                    catch
                    {
                        continue;
                    }

                    var types = asm
                        .GetTypes()
                        .Where(t => !t.IsAbstract && typeof(IConsoleNetworkToolPlugin).IsAssignableFrom(t));

                    foreach (var t in types)
                    {
                        try
                        {
                            var plugin = (IConsoleNetworkToolPlugin)Activator.CreateInstance(t);
                            result.Add(plugin);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return result;
        }
    }
}

