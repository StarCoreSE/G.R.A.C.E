using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.VisualScripting;
using VRage.ModAPI;
using VRage.Utils;
using static Scripts.Structure;

namespace GraceFramework
{
    public partial class GridLogicSession
    {
        private int UpdateCounter = 0;
        private int UpdateInterval = 100;
        private enum UpdateTarget
        {
            Faction,
            Player,
            Both
        }

        #region Updates
        private void TrackNewGrids()
        {
            foreach (var grid in _grids.Values)
            {
                if (HasValidBeacon(grid) && !_trackedGrids.ContainsKey(grid.EntityId))
                {
                    _trackedGrids[grid.EntityId] = new GridInfo
                    {
                        Grid = grid,
                    };

                    grid.OnBlockAdded += Grid_OnBlockAdded;
                }
            }
        }

        private void UpdateTrackedGrids()
        {
            foreach (var gridInfo in _trackedGrids.Values)
            {
                if (gridInfo.Grid.MarkedForClose)
                    continue;

                if (gridInfo.ClassKey == 0)
                {
                    var logic = GetBeaconLogic(gridInfo.Grid);
                    if (logic == null)
                        continue;

                    if (logic.ClassKey.Value != 0)
                    {
                        gridInfo.ClassKey = logic.ClassKey.Value;
                        gridInfo.ClassName = logic.ClassName.Value;

                        var blockCount = new List<IMySlimBlock>();
                        gridInfo.Grid.GetBlocks(blockCount);

                        gridInfo.BlockCount = blockCount.Count;
                        gridInfo.Mass = gridInfo.Grid.Physics?.Mass ?? 0;
                    }

                    if (gridInfo.ClassKey != 0)
                    {
                        if (gridInfo.Grid.BigOwners != null && gridInfo.Grid.BigOwners.Count > 0)
                        {
                            long factionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(gridInfo.Grid.BigOwners.First())?.FactionId ?? 0;
                            long playerId = gridInfo.Grid.BigOwners.First();

                            UpdateClassCounts(gridInfo.Grid.EntityId, true);

                        }
                    }
                }

                UpdateGridStats(gridInfo);

                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(gridInfo.ClassKey, out classDefinition))
                {
                    MyAPIGateway.Utilities.ShowNotification(
                        $"GridStats: Class:[{gridInfo.ClassName}] " +
                        $"BlockCount:[{gridInfo.BlockCount}/{classDefinition.MaxBlockCount}] " +
                        $"Mass:[{gridInfo.Mass:N0}/{classDefinition.MaxClassWeight:N0}]",
                        15,
                        "White"
                    );
                }
            }
        }

        private void UpdateGridStats(GridInfo gridInfo)
        {
            UpdateCounter++;

            if (gridInfo.Grid != null)
            {
                int updateCount = (int)(gridInfo.Grid.EntityId % UpdateInterval);

                if (UpdateCounter % UpdateInterval == updateCount)
                {
                    var logic = GetBeaconLogic(gridInfo.Grid);
                    if (logic != null)
                    {
                        var blockCount = new List<IMySlimBlock>();
                        gridInfo.Grid.GetBlocks(blockCount);

                        gridInfo.BlockCount = blockCount.Count;
                        gridInfo.Mass = gridInfo.Grid.Physics?.Mass ?? 0;
                    }
                }
            }

            if (UpdateCounter >= int.MaxValue - UpdateInterval)
            {
                UpdateCounter = 0;
            }
        }
        #endregion

        #region Count Tracking
        private void UpdateClassCounts(long entityID, bool add, UpdateTarget target = UpdateTarget.Both)
        {
            GridInfo info;
            _trackedGrids.TryGetValue(entityID, out info);

            long playerId = info?.Grid?.BigOwners?.First() ?? 0;
            long factionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId)?.FactionId ?? 0;
            long classKey = info?.ClassKey ?? 0;

            InitializeClassLimits(factionId, playerId);

            if (classKey != 0)
            {
                if (target == UpdateTarget.Faction || target == UpdateTarget.Both)
                {
                    if (add)
                        _factionClassCounts[factionId][classKey]++;
                    else
                        _factionClassCounts[factionId][classKey] = Math.Max(0, _factionClassCounts[factionId][classKey] - 1);
                }

                if (target == UpdateTarget.Player || target == UpdateTarget.Both)
                {
                    if (add)
                        _playerClassCounts[playerId][classKey]++;
                    else
                        _playerClassCounts[playerId][classKey] = Math.Max(0, _playerClassCounts[playerId][classKey] - 1);
                }
            }
        }

        private void InitializeClassLimits(long factionId, long playerId)
        {
            if (!_factionClassCounts.ContainsKey(factionId))
            {
                _factionClassCounts[factionId] = new Dictionary<long, int>();
            }
            foreach (var classDef in _classDefinitions)
            {
                if (!_factionClassCounts[factionId].ContainsKey(classDef.Key))
                {
                    _factionClassCounts[factionId][classDef.Key] = 0;
                }
            }

            if (!_playerClassCounts.ContainsKey(playerId))
            {
                _playerClassCounts[playerId] = new Dictionary<long, int>();
            }
            foreach (var classDef in _classDefinitions)
            {
                if (!_playerClassCounts[playerId].ContainsKey(classDef.Key))
                {
                    _playerClassCounts[playerId][classDef.Key] = 0;
                }
            }
        }

        private void ShowPlayerClassCounts(long playerId)
        {
            if (!_playerClassCounts.ContainsKey(playerId))
            {
                MyAPIGateway.Utilities.ShowNotification($"No tracked classes for Player {MyAPIGateway.Players.TryGetIdentityId(playerId).DisplayName}", 15, "White");
                return;
            }

            var counts = _playerClassCounts[playerId];
            var messageBuilder = new System.Text.StringBuilder($"{MyAPIGateway.Players.TryGetIdentityId(playerId).DisplayName} Grids: ");

            foreach (var classEntry in counts)
            {
                long classKey = classEntry.Key;
                int count = classEntry.Value;

                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(classKey, out classDefinition))
                {
                    messageBuilder.Append($"[{classDefinition.ClassName}] {count}/{classDefinition.PerPlayerAmount}, ");
                }

            }

            if (messageBuilder.Length > 0)
                messageBuilder.Length -= 2;

            MyAPIGateway.Utilities.ShowNotification(messageBuilder.ToString(), 15, "White");
        }

        private void ShowFactionClassCounts(long factionId)
        {
            if (!_factionClassCounts.ContainsKey(factionId))
            {
                MyAPIGateway.Utilities.ShowNotification($"No tracked classes for Faction {MyAPIGateway.Session.Factions.TryGetFactionById(factionId).Tag}", 15, "White");
                return;
            }

            var counts = _factionClassCounts[factionId];
            var messageBuilder = new System.Text.StringBuilder($"{MyAPIGateway.Session.Factions.TryGetFactionById(factionId).Tag} Grids: ");

            foreach (var classEntry in counts)
            {
                long classKey = classEntry.Key;
                int count = classEntry.Value;

                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(classKey, out classDefinition))
                {
                    messageBuilder.Append($"[{classDefinition.ClassName}] {count}/{classDefinition.PerFactionAmount}, ");
                }

            }

            if (messageBuilder.Length > 0)
                messageBuilder.Length -= 2;

            MyAPIGateway.Utilities.ShowNotification(messageBuilder.ToString(), 15, "White");
        }
        #endregion

        #region Event Handlers
        private void OnFactionCreated(long factionId)
        {
            InitializeClassLimits(factionId, 0);

            foreach (var gridInfo in _trackedGrids.Values)
            {
                long playerId = gridInfo.Grid.BigOwners?.FirstOrDefault() ?? 0;
                long gridFactionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId)?.FactionId ?? 0;

                if (gridFactionId == factionId)
                {
                    UpdateClassCounts(gridInfo.Grid.EntityId, true, UpdateTarget.Faction);
                }
            }
        }
        #endregion
    }
}
