﻿using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using YamlDotNet.Comment;
using YamlDotNet.Serialization;

namespace YobotChart.Shared.Configuration
{
    public class AppSettings : IDisposable
    {
        public static class Files
        {
            public static string ConfigFile { get; } = Path.Combine(Directories.CurrentDir, "appsettings.yml");
        }

        public class Directories
        {
            public static string CurrentDir { get; } = AppDomain.CurrentDomain.BaseDirectory;
        }

        public AppSettings()
        {
            if (Default != null)
            {
                return;
            }

            Default = this;
        }

        [YamlMember(Alias = "general")]
        [Description("通常设置")]
        public GeneralSection General { get; set; } = new GeneralSection();

        public void Save()
        {
            lock (FileSaveLock)
            {
                var converter = new SerializerBuilder()
                        .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
                        .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
                        .Build();
                //builder.WithTagMapping("tag:yaml.org,2002:test", typeof(Test));
                var content = converter.Serialize(this);
                File.WriteAllText(Files.ConfigFile, content);
            }
        }

        public void Dispose()
        {
            //foreach (var fs in FileStream.Values) fs?.Dispose();
            //FileStream?.Dispose();
        }

        private static readonly Encoding Encoding = Encoding.UTF8;
        private static readonly object FileSaveLock = new object();

        public static AppSettings Default { get; private set; }

        public static void SaveDefault()
        {
            Default?.Save();
        }

        public static void LoadFromDefaultFile()
        {
            if (!File.Exists(Files.ConfigFile))
            {
                CreateNewConfig();
            }
            else
            {
                var content = File.ReadAllText(Files.ConfigFile);
                var builder = new YamlDotNet.Serialization.DeserializerBuilder();
                //builder.WithTagMapping("tag:yaml.org,2002:test", typeof(Test));
                var ymlDeserializer = builder.Build();
                Load(ymlDeserializer.Deserialize<AppSettings>(content));
            }

        }

        public static void Load(AppSettings config)
        {
            Default = config ?? new AppSettings();
        }

        private static void LoadNew()
        {
            File.WriteAllText(Files.ConfigFile, "");
            Load(new AppSettings());
        }

        public static void CreateNewConfig()
        {
            LoadNew();
            SaveDefault();
        }
    }
}