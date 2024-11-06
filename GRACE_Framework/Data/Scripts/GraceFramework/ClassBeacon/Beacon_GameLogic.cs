using Sandbox.Common.ObjectBuilders;
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

        public MySync<string, SyncDirection.BothWays> ClassName;
        public MySync<long, SyncDirection.BothWays> ClassKey;

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

    }
}
