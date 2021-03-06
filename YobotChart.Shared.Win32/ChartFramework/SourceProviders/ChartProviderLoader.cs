﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using YobotChart.Shared.Configuration;
using YobotChart.Shared.Win32.ChartFramework.Attributes;
using YobotChart.Shared.Win32.ChartFramework.ConfigModels;
using YobotChart.Shared.Win32.ChartFramework.StatsProviders;

namespace YobotChart.Shared.Win32.ChartFramework.SourceProviders
{
    internal class ChartProviderLoader
    {
        public static HashSet<StatsProviderInfo> Load()
        {
            var defaultAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(k => k.FullName == "YobotChartProviders.default");
            if (defaultAsm == null)
            {
                var path = Path.Combine(AppSettings.Directories.CurrentDir, "YobotChartProviders.default.dll");
                if (File.Exists(path))
                {
                    defaultAsm = Assembly.LoadFile(path);
                }
            }

            var providers = Directory.GetFiles(AppSettings.Directories.ProvidersDir, "YobotChartProviders*.dll");
            var domain = AppDomain.CreateDomain("ProvidersDomain", null);
            List<Assembly> assemblies = new List<Assembly>();
            foreach (var provider in providers)
            {
                var ass = Assembly.LoadFile(provider);
                ass = domain.Load(ass.GetName());
                assemblies.Add(ass);
            }

            assemblies.Add(defaultAsm);
            var statsProviderInfos = new HashSet<StatsProviderInfo>();

            foreach (var asm in assemblies)
            {
                var statsProviders = asm.GetExportedTypes()
                    .Where(k => k.GetInterfaces().Contains(typeof(IStatsProvider)))
                    .ToArray();
                foreach (var statsProvider in statsProviders)
                {
                    try
                    {
                        var attr = statsProvider.GetCustomAttribute<StatsProviderAttribute>();
                        if (attr == null) continue;
                        var statisticsProviderInfo = new StatsProviderInfo
                        {
                            Guid = attr.Guid,
                            Name = attr.Name,
                            Author = attr.Author,
                            Description = attr.Description,
                            Version = attr.Version
                        };

                        statsProviderInfos.Add(statisticsProviderInfo);

                        var instance = (IStatsProvider)Activator.CreateInstance(statsProvider);
                        //instance.ChartProvider = this;
                        instance.YobotApiSource = YobotApiSource.Default;

                        var methods = statsProvider.GetMethods();
                        foreach (var methodInfo in methods)
                        {
                            var o = methodInfo.GetCustomAttribute<StatsMethodAttribute>();
                            if (o == null) continue;

                            var statsFunctionInfo = new StatsFunctionInfo(attr.Guid) { Name = o.Name, Guid = o.Guid };
                            var granularityAttr =
                                methodInfo.GetCustomAttribute<StatsMethodAcceptGranularityAttribute>();
                            if (granularityAttr != null)
                            {
                                statsFunctionInfo.AcceptGranularities = granularityAttr.AcceptGranularities;
                            }

                            var thumbnailAttr = methodInfo.GetCustomAttribute<StatsMethodThumbnailAttribute>();
                            if (thumbnailAttr != null)
                            {
                                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "providers",
                                    statisticsProviderInfo.Guid.ToString());
                                if (!Directory.Exists(dir))
                                    Directory.CreateDirectory(dir);
                                statsFunctionInfo.ThumbnailPath = Path.Combine(dir, thumbnailAttr.Path);
                            }

                            Func<GranularityModel, Task<IChartConfigModel>> invokeFunc = null;
                            var retType = methodInfo.ReturnType;
                            if (retType.IsGenericType)
                            {
                                var genericType = retType.GetGenericTypeDefinition();
                                if (genericType.IsSubclassOf(typeof(Task)))
                                {
                                    var genericArgs = retType.GetGenericArguments();
                                    if (genericArgs.Length == 1 &&
                                        genericArgs[0].GetInterfaces().Contains(typeof(IChartConfigModel)))
                                    {
                                        var args = methodInfo.GetParameters();
                                        if (args.Length == 1 && args[0].ParameterType == typeof(GranularityModel))
                                        {
                                            invokeFunc = async (granularity) =>
                                            {
                                                var task = (Task)methodInfo.Invoke(instance,
                                                    new object[] { granularity });
                                                await task.ConfigureAwait(false);
                                                var resultProperty = task.GetType().GetProperty("Result");
                                                return (IChartConfigModel)resultProperty?.GetValue(task);
                                            };
                                        }
                                        else
                                        {
                                            invokeFunc = async (granularity) =>
                                            {
                                                var task = (Task)methodInfo.Invoke(instance, null);
                                                await task.ConfigureAwait(false);
                                                var resultProperty = task.GetType().GetProperty("Result");
                                                return (IChartConfigModel)resultProperty?.GetValue(task);
                                            };
                                        }
                                    }
                                }
                            }
                            else if (retType.GetInterfaces().Contains(typeof(IChartConfigModel)))
                            {
                                var args = methodInfo.GetParameters();
                                if (args.Length == 1 && args[0].ParameterType == typeof(GranularityModel))
                                {
                                    invokeFunc = async (granularity) =>
                                    {
                                        await Task.CompletedTask;
                                        var configModel =
                                            (IChartConfigModel)methodInfo.Invoke(instance, new object[] { granularity });
                                        return configModel;
                                    };
                                }
                                else
                                {
                                    invokeFunc = async (granularity) =>
                                    {
                                        await Task.CompletedTask;
                                        var configModel = (IChartConfigModel)methodInfo.Invoke(instance, null);
                                        return configModel;
                                    };
                                }
                            }

                            if (invokeFunc != null)
                            {
                                statsFunctionInfo.Function = invokeFunc;
                            }

                            statisticsProviderInfo.FunctionList.Add(statsFunctionInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        // continue
                    }
                }
            }

            AppDomain.Unload(domain);
            return statsProviderInfos;
        }
    }
}