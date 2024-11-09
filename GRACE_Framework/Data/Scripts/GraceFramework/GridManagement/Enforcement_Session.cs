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
        public Dictionary<long, IMyCubeGrid> _gridsInViolation = new Dictionary<long, IMyCubeGrid>(); // EntityID + Grid
        private bool _clearedViolations;

        private StringBuilder violationMessage;
        private static bool ShowViolationMission;

        #region Event Handlers
        private void Grid_OnBlockAdded(IMySlimBlock block)
        {
            var fatBlock = block?.FatBlock;
            if (fatBlock == null)
                return;

            var classBeacon = fatBlock.CubeGrid.GetFatBlocks<IMyBeacon>().Where(b => b.BlockDefinition.SubtypeName == "LargeBlockBeacon").FirstOrDefault();
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

        private void Grid_OnBlockRemoved(IMySlimBlock block)
        {
            var fatBlock = block?.FatBlock;
            if (fatBlock == null || fatBlock.BlockDefinition.SubtypeName != "LargeBlockBeacon") 
                return;

            var grid = block?.CubeGrid;
            if (grid == null)
                return;

            GridInfo info;
            if (_trackedGrids.TryGetValue(grid.EntityId, out info))
            {
                if (info.ClassKey != 0)
                {
                    UpdateClassCounts(grid.EntityId, false);
                    info = null;
                }

                _gridsInViolation.Remove(grid.EntityId);
                _trackedGrids.Remove(grid.EntityId);
            }
        }

        private void DisplayViolationMission()
        {
            var title = CountMessageMode == DisplayMode.Player ? "Player Grid Violations" : "Faction Grid Violations";
            MyAPIGateway.Utilities.ShowMissionScreen(title, "", "Grids in Violation will be Deleted on Unload/Restart", violationMessage.ToString());
            ShowViolationMission = false;
        }
        #endregion

        #region Updates
        private void ClearViolatingGridsOnce()
        {
            if (!_clearedViolations)
            {
                if (LoadViolations())
                {
                    _gridsInViolation.Keys.ToList().ForEach(key =>
                    {
                        var grid = _gridsInViolation[key];
                        grid.Close();
                        _gridsInViolation.Remove(key);
                    });

                    MyLog.Default.WriteLine($"[GRACE] ClearViolations: Violations Still On List {_gridsInViolation.Count()}");
                    _clearedViolations = true;
                }

                _clearedViolations = true;
            }
        }

        private void EnforceViolations()
        {
            foreach (var gridInfo in _trackedGrids.Values)
            {
                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(gridInfo.ClassKey, out classDefinition))
                {
                    bool violationFound = false;

                    if (CheckClassLimits(gridInfo, classDefinition, null))
                        violationFound = true;

                    if (CheckBlockCount(gridInfo, classDefinition, null))
                        violationFound = true;

                    if (CheckMass(gridInfo, classDefinition, null))
                        violationFound = true;

                    if (CheckBlacklist(gridInfo, classDefinition, null))
                        violationFound = true;

                    if (violationFound)
                    {
                        if (!_gridsInViolation.ContainsKey(gridInfo.Grid.EntityId))
                        {
                            _gridsInViolation.Add(gridInfo.Grid.EntityId, gridInfo.Grid);
                            SaveViolations();
                        }                         
                    }
                    else
                    {
                        if (_gridsInViolation.ContainsKey(gridInfo.Grid.EntityId))
                        {
                            _gridsInViolation.Remove(gridInfo.Grid.EntityId);
                            SaveViolations();
                        }                    
                    }
                }
            }
        }

        private void ViolationMessage(DisplayMode displayMode, long playerId)
        {
            if (violationMessage == null)
            {
                violationMessage = new StringBuilder();
            }
            violationMessage.Clear();

            foreach (var gridInfo in _trackedGrids.Values)
            {
                if (!_gridsInViolation.ContainsKey(gridInfo.Grid.EntityId))
                    continue;

                bool includeGrid = displayMode == DisplayMode.Player
                    ? gridInfo.OwnerID == playerId 
                    : MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId).FactionId == MyAPIGateway.Session.Factions.TryGetPlayerFaction(gridInfo.OwnerID).FactionId;

                if (!includeGrid)
                    continue;

                if (displayMode == DisplayMode.Faction)
                {
                    var ownerName = MyAPIGateway.Players.TryGetIdentityId(gridInfo.OwnerID)?.DisplayName;
                    violationMessage.Append($"{gridInfo.Grid.DisplayName} (Owned By: {ownerName}) Violated Class Rules!\n");
                }
                else
                {
                    violationMessage.Append($"{gridInfo.Grid.DisplayName} Violated Class Rules!\n");
                }

                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(gridInfo.ClassKey, out classDefinition))
                {

                    CheckClassLimits(gridInfo, classDefinition, violationMessage);

                    CheckBlockCount(gridInfo, classDefinition, violationMessage);

                    CheckMass(gridInfo, classDefinition, violationMessage);

                    CheckBlacklist(gridInfo, classDefinition, violationMessage);

                    violationMessage.Append('\n');
                }
            }
        }
        #endregion

        #region Helpers
        private bool CheckClassLimits(GridInfo gridInfo, ClassDefinition classDefinition, StringBuilder messageBuilder)
        {
            bool violated = false;

            int playerClassCount = _playerClassCounts[gridInfo.Grid.BigOwners.First()][gridInfo.ClassKey];
            int factionClassCount = _factionClassCounts[MyAPIGateway.Session.Factions.TryGetPlayerFaction(gridInfo.Grid.BigOwners.First()).FactionId][gridInfo.ClassKey];

            if (playerClassCount > classDefinition.PlayerLimit || factionClassCount > classDefinition.FactionLimit)
            {
                if (messageBuilder != null)
                    messageBuilder.Append($" - [Limit Exceeded for Grids of Class {gridInfo.ClassName}] \n");

                violated = true;
            }

            return violated;
        }

        private bool CheckBlockCount(GridInfo gridInfo, ClassDefinition classDefinition, StringBuilder messageBuilder)
        {
            bool violated = false;

            if (gridInfo.BlockCount > classDefinition.MaxBlockCount)
            {
                if (messageBuilder != null)
                    messageBuilder.Append(" - [Maximum Block Count Exceeded!] \n" + $"    Current: {gridInfo.BlockCount:N0}\n    Max: {classDefinition.MaxBlockCount:N0}\n");
                
                violated = true;
            }
            else if (gridInfo.BlockCount < classDefinition.MinBlockCount)
            {
                if (messageBuilder != null)
                    messageBuilder.Append(" - [Minimum Block Count Not Met!] \n" + $"    Current: {gridInfo.BlockCount:N0}\n    Min: {classDefinition.MinBlockCount:N0}\n");
                
                violated = true;
            }

            return violated;
        }

        private bool CheckMass(GridInfo gridInfo, ClassDefinition classDefinition, StringBuilder messageBuilder)
        {
            bool violated = false;

            if (gridInfo.Mass > classDefinition.MaxClassWeight)
            {
                if (messageBuilder != null)
                    messageBuilder.Append(" - [Maximum Weight Exceeded!] \n" + $"    Current: {gridInfo.Mass:N0}\n    Max: {classDefinition.MaxClassWeight:N0}\n");
                
                violated = true;
            }
            else if (gridInfo.Mass < classDefinition.MinClassWeight)
            {
                if (messageBuilder != null)
                    messageBuilder.Append(" - [Minimum Weight Not Met!] \n" + $"    Current: {gridInfo.Mass:N0}\n    Min: {classDefinition.MinClassWeight:N0}\n");
                
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
                if (messageBuilder != null)
                    messageBuilder.Append(" - [Grid Has Blacklisted Blocks!] \n");
                
                violated = true;
            }

            return violated;
        }
        #endregion

        #region Save/Load
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
                if (serializedData == null || serializedData.Length == 0)
                {
                    MyLog.Default.WriteLine($"[GRACE] SaveViolations: Serialization returned empty or null data.");
                    return;
                }

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
                    MyLog.Default.WriteLine($"[GRACE] LoadViolations failed: MyAPIGateway.Utilities is null.");
                    return false;
                }

                var reader = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage("ViolationsData.bin", typeof(GridLogicSession));
                using (reader)
                {
                    if (reader == null || reader.BaseStream.Length == 0)
                    {
                        MyLog.Default.WriteLine($"[GRACE] LoadViolations failed: File ViolationsData.bin does not exist or has no data.");
                        return false;
                    }

                    byte[] data = new byte[reader.BaseStream.Length];
                    reader.Read(data, 0, data.Length);
                    var settings = MyAPIGateway.Utilities.SerializeFromBinary<SavedViolations>(data);
                    if (settings?.Saved_gridsInViolation != null)
                    {
                        _gridsInViolation = settings.Saved_gridsInViolation
                            .Select(id => MyAPIGateway.Entities.GetEntityById(id) as IMyCubeGrid)
                            .Where(grid => grid != null)
                            .ToDictionary(grid => grid.EntityId);

                        MyLog.Default.WriteLine($"[GRACE] LoadViolations: Successfully loaded violation data from session storage.");
                        return true;
                    }
                    else
                    {
                        MyLog.Default.WriteLine($"[GRACE] LoadViolations: Deserialized settings object is null or missing Saved_gridsInViolation data.");
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"[GRACE] Error loading violation data!\n{e}");
            }

            return false;
        }
        #endregion
    }

    [ProtoContract]
    public class SavedViolations
    {
        [ProtoMember(91)]
        public List<long> Saved_gridsInViolation { get; set; } // Store as a list of grid IDs
    }
}
