using System;
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
    public class GridLogicSession : MySessionComponentBase
    {
        public static GridLogicSession Instance;

        private Dictionary<long, IMyCubeGrid> _trackedGrids = new Dictionary<long, IMyCubeGrid>(); // EntityID, Grid
        private Dictionary<long, GridStats> _trackedGridStats = new Dictionary<long, GridStats>(); // EntityID, GridStats
        private Dictionary<long, Dictionary<long, int>> _trackedFactionGrids = new Dictionary<long, Dictionary<long, int>>(); // EntityID, ClassKey + Count
        private Dictionary<long, ClassDefinition> _classDefinitions = new Dictionary<long, ClassDefinition>(); // EntityID, ClassDefinitions

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

            if (grid != null && grid.GetFatBlocks<IMyBeacon>().Any(block => (block.BlockDefinition.SubtypeName == "LargeBlockBeacon" && ClassBeacon.GetLogic<ClassBeacon>(block.EntityId) != null)))
            {
                _trackedGrids.Add(grid.EntityId, grid);
                grid.OnMarkForClose += GridMarkedForClose;
            }
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            _trackedGrids.Remove(ent.EntityId);
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                foreach (var grid in _trackedGrids.Values)
                {
                    if (grid.MarkedForClose)
                        continue;

                    // EEEEE
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

        public static List<ClassDefinition> GetClassDefinitions()
        {
            return Instance?._classDefinitions.Values.ToList() ?? new List<ClassDefinition>();
        }
    }

    public class GridStats
    {
        public string ClassName { get; set; }
        public int ClassKey { get; set; }
        
        public int BlockCount { get; set; }
        public int Mass { get; set; }

        public int TurretedWeaponCount { get; set; }
        public int FixedWeaponCount { get; set; }

        public int CombatPoints { get; set; }
        public int UtilityPoints { get; set; }
    }
}