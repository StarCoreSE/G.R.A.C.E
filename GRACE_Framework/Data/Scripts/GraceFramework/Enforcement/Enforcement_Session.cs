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

        }


    }
}
