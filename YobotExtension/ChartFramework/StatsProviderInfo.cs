﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YobotExtension.ChartFramework
{
    public class StatsProviderInfo
    {
        public StatsProviderMetadataAttribute Metadata { get; set; }

        public Dictionary<StatsMethodAttribute, StatsFunctionInfo> FunctionsMapping
        {
            get;
            set;
        } = new Dictionary<StatsMethodAttribute, StatsFunctionInfo>();
    }

    public class StatsFunctionInfo
    {
        public Func<GranularityModel, Task<IChartConfigModel>> Function { get; set; }
        public GranularityType[] AcceptGranularities { get; set; }
        public string ThumbnailPath { get; set; }
    }
}