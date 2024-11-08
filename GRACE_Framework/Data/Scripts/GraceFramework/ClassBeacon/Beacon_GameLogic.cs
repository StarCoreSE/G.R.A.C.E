using System;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;

namespace GraceFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false)]
    public class ClassBeacon : MyGameLogicComponent
    {
        public IMyCubeBlock Block;
        public readonly Guid SettingsID = new Guid("25B52BE2-6D49-4A30-ACEE-FC8727F76F58");

        private bool ClientSettingsLoaded = false;

        public MySync<string, SyncDirection.BothWays> ClassName;
        public MySync<long, SyncDirection.BothWays> ClassKey;

        #region Overrides
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Block = (IMyCubeBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (Block?.CubeGrid?.Physics == null)
                return;

            ClassBeaconControls.DoOnce(ModContext);

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }       

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            if (MyAPIGateway.Utilities.IsDedicated)
            {
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_100TH_FRAME;
                return;
            }

            LoadSettings();

            ClassName.ValueChanged += OnVariableChangeString;
            ClassKey.ValueChanged += OnVariableChangeLong;

            NeedsUpdate &= ~MyEntityUpdateEnum.EACH_100TH_FRAME;
            return;
        }      

        public override void Close()
        {
            var I = GridLogicSession.Instance;
            if (I != null)
            {
                if (I._trackedGrids.ContainsKey(Block.CubeGrid.EntityId))
                {
                    I._trackedGrids.Remove(Block.CubeGrid.EntityId);
                }
            }

            base.Close();
        }
        #endregion

        #region Helpers
        public static T GetLogic<T>(long entityId) where T : MyGameLogicComponent
        {
            IMyEntity targetEntity = MyAPIGateway.Entities.GetEntityById(entityId);
            if (targetEntity == null)
            {
                return null;
            }

            IMyFunctionalBlock targetBlock = targetEntity as IMyFunctionalBlock;
            if (targetBlock == null)
            {
                return null;
            }

            var logic = targetBlock.GameLogic?.GetAs<T>();
            if (logic == null)
            {

            }

            return logic;
        }
        #endregion

        #region Settings
        private void OnVariableChangeString(MySync<string, SyncDirection.BothWays> sync)
        {
            SaveSettings();
        }

        private void OnVariableChangeLong(MySync<long, SyncDirection.BothWays> sync)
        {
            SaveSettings();
        }

        bool LoadSettings()
        {
            if (Block.Storage == null)
            {
                return false;
            }

            string rawData;
            if (!Block.Storage.TryGetValue(SettingsID, out rawData))
            {
                return false;
            }

            try
            {
                var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<BeaconSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    ClassName.Value = loadedSettings.Stored_ClassName;
                    ClassKey.Value = loadedSettings.Stored_ClassKey;

                    return true;
                }
            }
            catch (Exception e)
            {
                // bad practice go brrr
            }

            return false;
        }

        void SaveSettings()
        {
            if (Block == null)
            {
                return;
            }

            try
            {
                if (MyAPIGateway.Utilities == null)
                    throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId};");

                if (Block.Storage == null)
                {
                    Block.Storage = new MyModStorageComponent();
                }

                var settings = new BeaconSettings
                {
                    Stored_ClassName = ClassName.Value,
                    Stored_ClassKey = ClassKey.Value,
                };

                string serializedData = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(settings));
                Block.Storage.SetValue(SettingsID, serializedData);
            }
            catch (Exception e)
            {
                // bad practice go brrr
            }
        }
        #endregion

    }

    [ProtoContract]
    public class BeaconSettings
    {
        [ProtoMember(21)]
        public string? Stored_ClassName { get; set; }

        [ProtoMember(22)]
        public long Stored_ClassKey { get; set; }
    }
}
