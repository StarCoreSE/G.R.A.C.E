using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using static Scripts.Structure;

namespace Scripts
{
    public partial class Parts
    {
        internal ContainerDefinition Container = new ContainerDefinition();

        internal void AddClassDefinitions(params ClassDefinition[] defs)
        {
            Container.ClassDefinitions = defs;
        }

        internal static void GetBaseDefinitions(out ContainerDefinition baseDefs)
        {
            baseDefs = new Parts().Container;
        }

        internal static void SetModPath(ContainerDefinition baseDefs, string modContext)
        {
            if (baseDefs.ClassDefinitions != null)
                for (int i = 0; i < baseDefs.ClassDefinitions.Length; i++)
                    baseDefs.ClassDefinitions[i].ModPath = modContext;
        }
    }
}