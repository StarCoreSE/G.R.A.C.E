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

namespace GraceFramework
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class GridLogicSession : MySessionComponentBase
    {
        public static GridLogicSession Instance;

        private Dictionary<long, IMyCubeGrid> _grids = new Dictionary<long, IMyCubeGrid>(); // EntityID, Grid
        private Dictionary<long, IMyCubeGrid> _trackedGrids = new Dictionary<long, IMyCubeGrid>(); // EntityID, Grid

        private Dictionary<long, GridStats> _trackedGridStats = new Dictionary<long, GridStats>(); // EntityID, GridStats
        private Dictionary<long, Dictionary<long, int>> _trackedFactionGrids = new Dictionary<long, Dictionary<long, int>>(); // EntityID, ClassKey + Count

        private Dictionary<long, ClassDefinition> _classDefinitions = new Dictionary<long, ClassDefinition>(); // ClassKey, ClassDefinition

        public override void LoadData()
        {
            Instance = this;

            MyAPIGateway.Utilities.RegisterMessageHandler(6831, OnReceivedDefinitions);

            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(6831, OnReceivedDefinitions);

            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

            _trackedGrids.Clear();
            _classDefinitions.Clear();
        }

        private void EntityAdded(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null)
                return;

            if (HasValidBeacon(grid))
            {
                MyAPIGateway.Utilities.ShowMessage("EntityAdded", $"Found Valid Beacon");
                _trackedGrids[grid.EntityId] = grid;
                if (GetBeaconLogic(grid).ClassKey.Value != 0 && !_trackedGridStats.ContainsKey(grid.EntityId))
                {
                    var logic = GetBeaconLogic(grid);
                    var blockCount = new List<IMySlimBlock>();
                    grid.GetBlocks(blockCount);
                    _trackedGridStats[grid.EntityId] = new GridStats()
                    {
                        ClassKey = logic.ClassKey.Value,
                        ClassName = logic.ClassName.Value,

                        BlockCount = blockCount.Count(),
                        Mass = grid.Physics.Mass,
                    };
                }
                
                grid.OnBlockAdded += Grid_OnBlockAdded;
            }

            _grids[grid.EntityId] = grid;
            grid.OnMarkForClose += GridMarkedForClose;
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            _grids.Remove(ent.EntityId);

            if (_trackedGrids.Keys.Contains(ent.EntityId))
            {
                _trackedGrids.Remove(ent.EntityId);
            }
            
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                foreach (var grid in _grids.Values)
                {
                    if (HasValidBeacon(grid) && !_trackedGrids.ContainsKey(grid.EntityId))
                    {
                        MyAPIGateway.Utilities.ShowMessage("BeforeSimulation", $"Found Grid to Track");
                        _trackedGrids[grid.EntityId] = grid;
                        grid.OnBlockAdded += Grid_OnBlockAdded;
                    }
                }

                foreach (var grid in _trackedGrids.Values)
                {
                    if (grid.MarkedForClose)
                        continue;

                    if (GetBeaconLogic(grid).ClassKey.Value != 0 && !_trackedGridStats.ContainsKey(grid.EntityId))
                    {
                        var logic = GetBeaconLogic(grid);
                        var blockCount = new List<IMySlimBlock>();
                        grid.GetBlocks(blockCount);
                        _trackedGridStats[grid.EntityId] = new GridStats()
                        {
                            ClassKey = logic.ClassKey.Value,
                            ClassName = logic.ClassName.Value,

                            BlockCount = blockCount.Count(),
                            Mass = grid.Physics.Mass,
                        };
                    }

                    GridStats gridStats;
                    ClassDefinition classDefinition;             
                    if (_trackedGridStats.TryGetValue(grid.EntityId, out gridStats) && _classDefinitions.TryGetValue(GetBeaconLogic(grid).ClassKey, out classDefinition)) 
                    {

                        MyAPIGateway.Utilities.ShowNotification($"GridStats: Class:[{gridStats.ClassName}] BlockCount:[{gridStats.BlockCount}/{classDefinition.MaxBlockCount}] Mass:[{gridStats.Mass}/{classDefinition.MaxClassWeight}]", 15, "White");
                    }

                    // LimitViolationEnforcement();
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
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
                                MyLog.Default.WriteLine($"[GlobalGridClasses] Duplicate ClassKey found: {definition.ClassKey} for Class: {definition.ClassName}. Skipping addition.");
                            }
                        }

                        MyLog.Default.WriteLine($"[GlobalGridClasses] Successfully received and stored {container.ClassDefinitions.Length} definitions.");
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[GlobalGridClasses] Error in receiving definitions: {ex.Message}");
            }
        }

        private bool HasValidBeacon(IMyCubeGrid grid)
        {
            return grid.GetFatBlocks<IMyBeacon>()
                       .Any(block => block.BlockDefinition.SubtypeName == "LargeBlockBeacon"
                                     && ClassBeacon.GetLogic<ClassBeacon>(block.EntityId) != null);
        }

        private ClassBeacon GetBeaconLogic(IMyCubeGrid grid)
        {
            var block = grid.GetFatBlocks<IMyBeacon>().Where(b => b.BlockDefinition.SubtypeName == "LargeBlockBeacon").First();
            return ClassBeacon.GetLogic<ClassBeacon>(block.EntityId);
        }

        public static List<ClassDefinition> GetClassDefinitions()
        {
            return Instance?._classDefinitions.Values.ToList() ?? new List<ClassDefinition>();
        }
    }

    public class GridStats
    {
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