using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using static Scripts.Structure;

namespace GraceFramework
{
    public partial class GridLogicSession
    {
        private void Grid_OnBlockAdded(IMySlimBlock block)
        {
            var fatBlock = block?.FatBlock;
            if (fatBlock == null)
                return;

            var classBeacon = fatBlock.CubeGrid.GetFatBlocks<IMyBeacon>().Where(b => b.BlockDefinition.SubtypeName == "LargeBlockBeacon").First();
            if (classBeacon != null)
            {
                var beaconLogic = ClassBeacon.GetLogic<ClassBeacon>(classBeacon.EntityId);
                if (beaconLogic != null)
                {
                    ClassDefinition classDefinition;
                    if (_classDefinitions.TryGetValue(beaconLogic.ClassKey, out classDefinition))
                    {
                        if (classDefinition.BlacklistedBlocks.Contains(fatBlock.BlockDefinition.SubtypeName))
                        {
                            block.CubeGrid.RemoveBlock(block, true);
                            MyAPIGateway.Utilities.ShowNotification($"Block [{fatBlock.BlockDefinition.SubtypeName}] is Prohibited for [{classDefinition.ClassName}] Class Ships!", 1800, "Red");
                        }
                    }
                }
            }
        }

        private void LimitViolationEnforcement()
        {
            foreach ( var gridInfo in _trackedGrids.Values)
            {      
                var messageBuilder = new StringBuilder($"Grid {gridInfo.Grid.DisplayName} Violated Class Rules! For:");

                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(gridInfo.ClassKey, out classDefinition))
                {
                    bool violationFound = false;

                    if (CheckBlockCount(gridInfo, classDefinition, messageBuilder)) 
                        violationFound = true;

                    if (CheckMass(gridInfo, classDefinition, messageBuilder)) 
                        violationFound = true;

                    if (violationFound)
                    {
                        MyAPIGateway.Utilities.ShowNotification(messageBuilder.ToString(), 15, "Red");
                    }
                }
            }
        }

        private bool CheckBlockCount(GridInfo gridInfo, ClassDefinition classDefinition, StringBuilder messageBuilder)
        {
            bool violated = false;

            if (gridInfo.BlockCount > classDefinition.MaxBlockCount)
            {
                messageBuilder.Append(" [Maximum Block Count Exceeded!]");
                violated = true;
            }
            else if (gridInfo.BlockCount < classDefinition.MinBlockCount)
            {
                messageBuilder.Append(" [Minimum Block Count Not Met!]");
                violated = true;
            }

            return violated;
        }

        private bool CheckMass(GridInfo gridInfo, ClassDefinition classDefinition, StringBuilder messageBuilder)
        {
            bool violated = false;

            if (gridInfo.Mass > classDefinition.MaxClassWeight)
            {
                messageBuilder.Append(" [Maximum Weight Exceeded!]");
                violated = true;
            }
            else if (gridInfo.Mass < classDefinition.MinClassWeight)
            {
                messageBuilder.Append(" [Minimum Weight Not Met!]");
                violated = true;
            }

            return violated;
        }
    }
}
