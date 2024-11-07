using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
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
        public Dictionary<long, IMyCubeGrid> _gridsInViolation = new Dictionary<long, IMyCubeGrid>();

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
            foreach (var gridInfo in _trackedGrids.Values)
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

                    if (CheckBlacklist(gridInfo, classDefinition, messageBuilder))
                        violationFound = true;

                    if (violationFound)
                    {
                        if (!_gridsInViolation.ContainsKey(gridInfo.Grid.EntityId))
                            _gridsInViolation.Add(gridInfo.Grid.EntityId, gridInfo.Grid);

                        MyAPIGateway.Utilities.ShowNotification(messageBuilder.ToString(), 15, "Red");
                    }
                    else
                    {
                        if (_gridsInViolation.ContainsKey(gridInfo.Grid.EntityId))
                            _gridsInViolation.Remove(gridInfo.Grid.EntityId);
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

        private bool CheckBlacklist(GridInfo gridInfo, ClassDefinition classDefinition, StringBuilder messageBuilder)
        {
            bool violated = false;

            var functionalBlocks = gridInfo.Grid.GetFatBlocks<IMyFunctionalBlock>();
            if (functionalBlocks.Any(block => classDefinition.BlacklistedBlocks.Contains(block.BlockDefinition.SubtypeName)))
            {
                messageBuilder.Append(" [Grid Has Blacklisted Blocks!]");
                violated = true;
            }

            return violated;
        }

        private void SaveViolations()
        {
            try
            {
                if (MyAPIGateway.Utilities == null)
                {
                    MyLog.Default.WriteLine($"[GRACE] SaveViolations failed: MyAPIGateway.Utilities is null.");
                    return;
                }

                var settings = new SavedViolations
                {
                    Saved_gridsInViolation = _gridsInViolation.Keys.ToList() 
                };

                byte[] serializedData = MyAPIGateway.Utilities.SerializeToBinary(settings);
                using (var writer = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage("ViolationsData.bin", typeof(GridLogicSession)))
                {
                    writer.Write(serializedData, 0, serializedData.Length);
                }

                MyLog.Default.WriteLine($"[GRACE] SaveViolations: Successfully saved violation data to session storage.");
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"[GRACE] Error saving violation data!\n{e}");
            }
        }

        private bool LoadViolations()
        {
            try
            {
                if (MyAPIGateway.Utilities == null)
                {
                    MyLog.Default.WriteLine($"[GRACE] SaveViolations failed: MyAPIGateway.Utilities is null.");
                    return false;
                }

                var reader = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage("ViolationsData.bin", typeof(GridLogicSession));
                using (reader)
                {
                    if (reader.BaseStream.Length == 0)
                        return false;

                    byte[] data = new byte[reader.BaseStream.Length];
                    reader.Read(data, 0, data.Length);

                    var settings = MyAPIGateway.Utilities.SerializeFromBinary<SavedViolations>(data);

                    if (settings?.Saved_gridsInViolation != null)
                    {
                        _gridsInViolation = settings.Saved_gridsInViolation
                            .Select(id => MyAPIGateway.Entities.GetEntityById(id) as IMyCubeGrid)
                            .Where(grid => grid != null)
                            .ToDictionary(grid => grid.EntityId);

                        MyLog.Default.WriteLine($"[GRACE] SaveViolations: Successfully loaded violation data from session storage.");
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"[GRACE] Error loading violation data!\n{e}");
            }

            return false;
        }
    }

    [ProtoContract]
    public class SavedViolations
    {
        [ProtoMember(1)]
        public List<long> Saved_gridsInViolation { get; set; } // Store as a list of grid IDs
    }
}
