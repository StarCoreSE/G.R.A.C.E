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
        private void TrackNewGrids()
        {
            foreach (var grid in _grids.Values)
            {
                if (HasValidBeacon(grid) && !_trackedGrids.ContainsKey(grid.EntityId))
                {
                    MyAPIGateway.Utilities.ShowMessage("BeforeSimulation", $"Found Grid to Track");

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

                ClassDefinition classDefinition;
                if (_classDefinitions.TryGetValue(gridInfo.ClassKey, out classDefinition))
                {
                    MyAPIGateway.Utilities.ShowNotification(
                        $"GridStats: Class:[{gridInfo.ClassName}] " +
                        $"BlockCount:[{gridInfo.BlockCount}/{classDefinition.MaxBlockCount}] " +
                        $"Mass:[{gridInfo.Mass}/{classDefinition.MaxClassWeight}]",
                        15,
                        "White"
                    );
                }
            }
        }

        private void UpdateClassCounts(long entityID, bool add)
        {
            GridInfo info;
            _trackedGrids.TryGetValue(entityID, out info);

            long factionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(info.Grid.BigOwners.First())?.FactionId ?? 0;
            long playerId = info.Grid.BigOwners.First();
            long classKey = info.ClassKey;

            // Update faction class count
            if (!_factionClassCounts.ContainsKey(factionId))
                _factionClassCounts[factionId] = new Dictionary<long, int>();

            if (!_factionClassCounts[factionId].ContainsKey(classKey))
                _factionClassCounts[factionId][classKey] = 0;

            if (add)
                _factionClassCounts[factionId][classKey]++;
            else
                _factionClassCounts[factionId][classKey]--;

            // Update player class count
            if (!_playerClassCounts.ContainsKey(playerId))
                _playerClassCounts[playerId] = new Dictionary<long, int>();

            if (!_playerClassCounts[playerId].ContainsKey(classKey))
                _playerClassCounts[playerId][classKey] = 0;

            if (add)
                _playerClassCounts[playerId][classKey]++;
            else
                _playerClassCounts[playerId][classKey]--;
        }

        private void ShowPlayerClassCounts(long playerId)
        {
            if (!_playerClassCounts.ContainsKey(playerId))
            {
                MyAPIGateway.Utilities.ShowNotification("No tracked classes for this player.", 15, "White");
                return;
            }

            var counts = _playerClassCounts[playerId];
            var messageBuilder = new System.Text.StringBuilder("PlayerCounts: ");

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
    }
}
