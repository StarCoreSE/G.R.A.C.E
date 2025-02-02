﻿using ProtoBuf;
using System.Linq;
using System.Collections.Generic;

namespace Scripts
{
    public class Structure
    {
        [ProtoContract]
        public class ContainerDefinition
        {
            [ProtoMember(1)] public ClassDefinition[] ClassDefinitions;
        }

        [ProtoContract]
        public struct ClassDefinition
        {
            [ProtoMember(1)] public string ModPath;

            [ProtoMember(2)] public string ClassName { get; set; }
            [ProtoMember(3)] public long ClassKey { get; set; }

            [ProtoMember(4)] public int FactionLimit { get; set; }
            [ProtoMember(5)] public int PlayerLimit { get; set; }

            [ProtoMember(6)] public int MaxBlockCount { get; set; }
            [ProtoMember(7)] public int MinBlockCount { get; set; }

            [ProtoMember(8)] public float MaxClassWeight { get; set; }
            [ProtoMember(9)] public float MinClassWeight { get; set; }

            [ProtoMember(10)] public List<string> BlacklistedBlocks { get; set; }

            public ClassDefinition(
                string className,
                long classKey,
                int perFactionAmount,
                int perPlayerAmount,
                int maxBlockCount,
                int minBlockCount,
                float maxClassWeight,
                float minClassWeight,
                List<string> blacklistedBlocks = null)
            {
                ModPath = null;
                ClassName = className;
                ClassKey = classKey;
                FactionLimit = perFactionAmount;
                PlayerLimit = perPlayerAmount;
                MaxBlockCount = maxBlockCount;
                MinBlockCount = minBlockCount;
                MaxClassWeight = maxClassWeight;
                MinClassWeight = minClassWeight;
                BlacklistedBlocks = blacklistedBlocks ?? new List<string>();
            }
        }
    }
}