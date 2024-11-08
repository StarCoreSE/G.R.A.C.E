using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Draygo.API;
using Microsoft.VisualBasic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.VisualScripting;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static Scripts.Structure;
using static VRageRender.MyBillboard;

namespace GraceFramework
{
    public partial class GridLogicSession
    {
        private enum UpdateTarget
        {
            Faction,
            Player,
            Both
        }

        public enum DisplayMode
        {
            None = 0,
            Player = 1,
            Faction = 2
        }

        private int UpdateCounter = 0;
        private int UpdateInterval = 100;

        Vector2D textPositionDefault = new Vector2D(-1, 1);
        const double textScaleDefault = 1;
        const string textFontDefault = "white";
        const bool textFontShadowDefault = false;
        HudAPIv2.HUDMessage CountHUDMessage;
        StringBuilder HUDMessageContent = null;

        public static DisplayMode CountMessageMode = DisplayMode.Player; 

        private readonly Dictionary<MyKeys, Action> _keyAndActionPairs = new Dictionary<MyKeys, Action>
        {
            [MyKeys.U] = () =>
            {
                CountMessageMode++;
                if (CountMessageMode > (DisplayMode)2)
                    CountMessageMode = 0;
                MyAPIGateway.Utilities.ShowNotification(
                    "GridCount Display Mode set to " + CountMessageMode);
            },
            [MyKeys.I] = () =>
            {
                ShowViolationMission = true;
            },
        };

        #region Updates
        private void HandleHUDMessage()
        {
            HandleKeyInputs();

            if (CountMessageMode == 0)
            {
                PurgeHUDMessage();
                return;
            }
            else if (CountMessageMode == (DisplayMode)1)
            {
                List<IMyPlayer> players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                foreach (var player in players)
                {
                    ShowClassCounts(player.IdentityId, CountMessageMode);
                }
            }
            else if (CountMessageMode == (DisplayMode)2)
            {
                List<IMyFaction> factions = new List<IMyFaction>();
                foreach (var faction in MyAPIGateway.Session.Factions.Factions.Where(faction => !faction.Value.IsEveryoneNpc()))
                {
                    ShowClassCounts(faction.Key, CountMessageMode);
                }
            }
        }

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
                    grid.OnBlockRemoved += Grid_OnBlockRemoved;
                    grid.OnBlockOwnershipChanged += Grid_OnOwnershipChange;
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

                            gridInfo.OwnerID = playerId;
                            gridInfo.OwnerFactionID = factionId;

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
            if (!_trackedGrids.TryGetValue(entityID, out info))
                return;

            long playerId = (info?.Grid?.BigOwners != null && info.Grid.BigOwners.Count > 0) ? info.Grid.BigOwners.First() : 0;
            long factionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId)?.FactionId ?? 0;
            long classKey = info?.ClassKey ?? 0;

            if (playerId == 0 || factionId == 0 || classKey == 0)
                return;

            InitializeClassLimits(factionId, playerId);

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

        private void ShowClassCounts(long Id, DisplayMode displayMode)
        {
            if (HUDMessageContent == null)
            {
                HUDMessageContent = new StringBuilder();
            }
            HUDMessageContent.Clear();

            var selectedDict = displayMode == DisplayMode.Player ? _playerClassCounts : _factionClassCounts;
            string entityName = displayMode == DisplayMode.Player 
                ? MyAPIGateway.Players.TryGetIdentityId(Id).DisplayName 
                : MyAPIGateway.Session.Factions.TryGetFactionById(Id).Tag;

            if (!selectedDict.ContainsKey(Id) || selectedDict[Id].All(kvp => kvp.Value == 0))
            {
                HUDMessageContent.Append($"No tracked classes for {(displayMode == DisplayMode.Player ? "Player" : "Faction")} {entityName} \n");
            }
            else
            {
                HUDMessageContent.Append($"{entityName} Grids: \n");

                var counts = selectedDict[Id];
                foreach (var classEntry in counts)
                {
                    long classKey = classEntry.Key;
                    int count = classEntry.Value;

                    ClassDefinition classDefinition;
                    if (_classDefinitions.TryGetValue(classKey, out classDefinition))
                    {
                        int maxCount = displayMode == DisplayMode.Player ? classDefinition.PerPlayerAmount : classDefinition.PerFactionAmount;
                        HUDMessageContent.Append($"[{classDefinition.ClassName}] {count}/{maxCount} \n");
                    }
                }
            }

            HUDMessageContent.Append("<color=yellow>Press 'Shift + U' To Cycle Display \n");

            if (displayMode == DisplayMode.Player)
            {
                bool playerHasViolatingGrids = _gridsInViolation.Any(kvp => kvp.Value.BigOwners.Contains(Id));
                if (playerHasViolatingGrids)
                {
                    HUDMessageContent.Append("<color=red>Player Grids Violating Class Rules! \nPress 'Shift + I' For Info!");
                }
            }
            else if (displayMode == DisplayMode.Faction)
            {
                bool factionHasViolatingGrids = _gridsInViolation.Any(kvp =>
                    kvp.Value.BigOwners.Any(owner => MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner)?.FactionId == Id)
                );
                if (factionHasViolatingGrids)
                {
                    HUDMessageContent.Append("<color=red>Faction Grids Violating Class Rules! \nPress 'Shift + I' For Info!");
                }
            }

            if (CountHUDMessage == null)
            {
                CountHUDMessage = new HudAPIv2.HUDMessage
                (
                    Message: HUDMessageContent,
                    Origin: textPositionDefault,
                    Offset: null,
                    TimeToLive: -1,
                    Scale: textScaleDefault,
                    HideHud: false,
                    Shadowing: textFontShadowDefault,
                    ShadowColor: Color.Black,
                    Blend: BlendTypeEnum.PostPP,
                    Font: textFontDefault
                );
            }
        }

        private void PurgeHUDMessage()
        {
            if (CountHUDMessage != null)
            {
                CountHUDMessage.Visible = false;
                CountHUDMessage.DeleteMessage();
                CountHUDMessage = null;
            }
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

        private void Grid_OnOwnershipChange(IMyCubeGrid grid)
        {
            GridInfo gridInfo;
            if (!_trackedGrids.TryGetValue(grid.EntityId, out gridInfo) || gridInfo.ClassKey == 0)
                return;

            long currentPlayerId = gridInfo.Grid.BigOwners.Count > 0 ? gridInfo.Grid.BigOwners.First() : 0;
            long currentFactionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(currentPlayerId)?.FactionId ?? 0;

            bool playerChanged = currentPlayerId != gridInfo.OwnerID;
            bool factionChanged = currentFactionId != gridInfo.OwnerFactionID;

            if (!playerChanged && !factionChanged)
                return;

            if (playerChanged && gridInfo.OwnerID != 0)
            {
                _playerClassCounts[gridInfo.OwnerID][gridInfo.ClassKey] =
                    Math.Max(0, _playerClassCounts[gridInfo.OwnerID][gridInfo.ClassKey] - 1);
            }

            if (factionChanged && gridInfo.OwnerFactionID != 0)
            {
                _factionClassCounts[gridInfo.OwnerFactionID][gridInfo.ClassKey] =
                    Math.Max(0, _factionClassCounts[gridInfo.OwnerFactionID][gridInfo.ClassKey] - 1);
            }

            if (playerChanged)
            {
                if (!_playerClassCounts.ContainsKey(currentPlayerId))
                    _playerClassCounts[currentPlayerId] = new Dictionary<long, int>();

                if (!_playerClassCounts[currentPlayerId].ContainsKey(gridInfo.ClassKey))
                    _playerClassCounts[currentPlayerId][gridInfo.ClassKey] = 0;

                _playerClassCounts[currentPlayerId][gridInfo.ClassKey]++;
            }

            if (factionChanged)
            {
                if (!_factionClassCounts.ContainsKey(currentFactionId))
                    _factionClassCounts[currentFactionId] = new Dictionary<long, int>();

                if (!_factionClassCounts[currentFactionId].ContainsKey(gridInfo.ClassKey))
                    _factionClassCounts[currentFactionId][gridInfo.ClassKey] = 0;

                _factionClassCounts[currentFactionId][gridInfo.ClassKey]++;
            }

            gridInfo.OwnerID = currentPlayerId;
            gridInfo.OwnerFactionID = currentFactionId;
        }

        private void HandleKeyInputs()
        {
            if (!MyAPIGateway.Input.IsAnyShiftKeyPressed())
                return;

            foreach (var pair in _keyAndActionPairs)
                if (MyAPIGateway.Input.IsNewKeyPressed(pair.Key))
                    pair.Value.Invoke();
        }
        #endregion
    }
}
