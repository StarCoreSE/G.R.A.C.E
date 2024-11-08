using System;
using System.Linq;
using System.Collections.Generic;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using static Scripts.Structure;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using static Scripts.Communication;

namespace GraceFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class GridLogicSession : MySessionComponentBase
    {
        public static GridLogicSession Instance;

        public Dictionary<long, IMyCubeGrid> _grids = new Dictionary<long, IMyCubeGrid>(); // EntityID, Grid
        public Dictionary<long, GridInfo> _trackedGrids = new Dictionary<long, GridInfo>(); // EntityID, GridInfo

        public Dictionary<long, Dictionary<long, int>> _factionClassCounts = new Dictionary<long, Dictionary<long, int>>(); // FactionID, ClassKey + Count
        public Dictionary<long, Dictionary<long, int>> _playerClassCounts = new Dictionary<long, Dictionary<long, int>>(); // PlayerID, ClassKey + Count

        public Dictionary<long, ClassDefinition> _classDefinitions = new Dictionary<long, ClassDefinition>(); // ClassKey, ClassDefinition

        #region Overrides
        public override void LoadData()
        {
            Instance = this;

            MyAPIGateway.Utilities.RegisterMessageHandler(6831, OnReceivedDefinitions);

            MyAPIGateway.Session.Factions.FactionCreated += OnFactionCreated;

            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                ClearViolatingGridsOnce();

                TrackNewGrids();

                UpdateTrackedGrids();

                List<IMyPlayer> players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                foreach (var player in players)
                {
                    ShowPlayerClassCounts(player.IdentityId);
                }

                List<IMyFaction> factions = new List<IMyFaction>();
                foreach (var faction in MyAPIGateway.Session.Factions.Factions.Where(faction => !faction.Value.IsEveryoneNpc()))
                {
                    ShowFactionClassCounts(faction.Key);
                }

                EnforceViolations();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(6831, OnReceivedDefinitions);

            MyAPIGateway.Session.Factions.FactionCreated -= OnFactionCreated;

            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

            SaveViolations();

            _trackedGrids.Clear();
            _classDefinitions.Clear();
        }
        #endregion

        #region Event Handlers
        private void EntityAdded(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null)
                return;

            _grids[grid.EntityId] = grid;
            grid.OnMarkForClose += GridMarkedForClose;

            if (!HasValidBeacon(grid) || _trackedGrids.ContainsKey(grid.EntityId))
                return;

            var logic = GetBeaconLogic(grid);
            var blockCount = new List<IMySlimBlock>();
            grid.GetBlocks(blockCount);

            _trackedGrids[grid.EntityId] = new GridInfo
            {
                Grid = grid,
                ClassKey = logic.ClassKey.Value,
                ClassName = logic.ClassName.Value,
                BlockCount = blockCount.Count,
                Mass = grid.Physics?.Mass ?? 0
            };

            if (grid.BigOwners != null && grid.BigOwners.Count > 0)
            {
                long factionId = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners.First())?.FactionId ?? 0;
                long playerId = grid.BigOwners.First();

                UpdateClassCounts(grid.EntityId, true);
            }

            grid.OnBlockAdded += Grid_OnBlockAdded;
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null)
                return;

            _grids.Remove(ent.EntityId);

            GridInfo info;
            if (_trackedGrids.TryGetValue(ent.EntityId, out info))
            {
                if (info.ClassKey != 0)
                {
                    UpdateClassCounts(grid.EntityId, false);
                    info = null;
                }

                _trackedGrids.Remove(grid.EntityId);
            }
        }

        private void OnReceivedDefinitions(object o)
        {
            try
            {
                var data = o as byte[];
                if (data != null)
                {
                    var container = MyAPIGateway.Utilities.SerializeFromBinary<ContainerDefinition>(data);
                    if (container?.ClassDefinitions != null)
                    {
                        foreach (var definition in container.ClassDefinitions)
                        {
                            if (!_classDefinitions.ContainsKey(definition.ClassKey))
                            {
                                _classDefinitions[definition.ClassKey] = definition;
                            }
                            else
                            {
                                MyLog.Default.WriteLine($"[GRACE] Duplicate ClassKey found: {definition.ClassKey} for Class: {definition.ClassName}. Skipping addition.");
                            }
                        }

                        MyLog.Default.WriteLine($"[GRACE] Successfully received and stored {container.ClassDefinitions.Length} definitions.");
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[GRACE] Error in receiving definitions: {ex.Message}");
            }
        }
        #endregion

        #region Helpers
        private bool HasValidBeacon(IMyCubeGrid grid)
        {
            return grid.GetFatBlocks<IMyBeacon>()
                       .Any(block => block.BlockDefinition.SubtypeName == "LargeBlockBeacon"
                                     && ClassBeacon.GetLogic<ClassBeacon>(block.EntityId) != null);
        }

        private ClassBeacon GetBeaconLogic(IMyCubeGrid grid)
        {
            if (grid == null || !grid.GetFatBlocks<IMyBeacon>().Any())
                return null;

            var block = grid.GetFatBlocks<IMyBeacon>().Where(b => b.BlockDefinition.SubtypeName == "LargeBlockBeacon").First();
            return ClassBeacon.GetLogic<ClassBeacon>(block.EntityId);
        }

        public static List<ClassDefinition> GetClassDefinitions()
        {
            return Instance?._classDefinitions.Values.ToList() ?? new List<ClassDefinition>();
        }
        #endregion
    }

    public class GridInfo
    {
        public IMyCubeGrid Grid { get; set; }

        public string ClassName { get; set; }
        public long ClassKey { get; set; }

        public int BlockCount { get; set; }
        public float Mass { get; set; }

        public int TurretedWeaponCount { get; set; }
        public int FixedWeaponCount { get; set; }

        public int CombatPoints { get; set; }
        public int UtilityPoints { get; set; }
    }
}