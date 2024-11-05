using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRage.Library.Collections;
using static Scripts.Structure;

namespace GraceFramework
{
    public static class ClassBeaconControls
    {
        const string IdPrefix = "ClassBeacon_";
        static bool Done = false;
        static string _selectedGridType;

        public static void DoOnce(IMyModContext context)
        {
            try
            {
                if (Done)
                    return;
                Done = true;


                CreateControls();
                CreateActions(context);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"[ClassBeacon] {e}");
            }
        }

        static bool IsVisible(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<ClassBeacon>() != null;
        }

        static bool IsClassNotSelected(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<ClassBeacon>().ClassKey.Value == 0;
        }

        static void CreateControls()
        {
            #region Class Selection Dropdown
            var SelectClassDropdown = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyBeacon>(IdPrefix + "SelectClassDropdown");
            SelectClassDropdown.Title = MyStringId.GetOrCompute("Class Selection:");
            SelectClassDropdown.Tooltip = MyStringId.GetOrCompute("Select Grid Class");
            SelectClassDropdown.Visible = IsVisible;
            SelectClassDropdown.Enabled = IsClassNotSelected;
            SelectClassDropdown.Getter = (b) =>
            {
                var logic = GetLogic(b);
                if (logic != null)
                {
                    return (logic.ClassKey.Value == 0) ? 0 : logic.ClassKey.Value;
                }

                return 0;
            };
            SelectClassDropdown.Setter = (b, key) =>
            {
                var logic = GetLogic(b);
                if (logic != null)
                {
                    logic.ClassKey.Value = key;
                    var beacon = b as IMyBeacon;
                    if (beacon != null)
                    {
                        var classDefinitions = GridLogicSession.GetClassDefinitions();
                        var matchingClass = classDefinitions.FirstOrDefault(classDef => classDef.ClassKey == key);

                        string shipClass = !matchingClass.Equals(default(ClassDefinition))
                        ? $"[{matchingClass.ClassName}] {logic.Block.CubeGrid.DisplayName}"
                        : $"[Unknown Class] {logic.Block.CubeGrid.DisplayName}";

                        beacon.HudText = shipClass;
                    }
                    SelectClassDropdown.UpdateVisual();
                }
            };
            SelectClassDropdown.ComboBoxContent = (list) =>
            {
                list.Add(new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute("None") });

                var classDefinitions = GridLogicSession.GetClassDefinitions();
                foreach (var classDef in classDefinitions)
                {
                    list.Add(new MyTerminalControlComboBoxItem
                    {
                        Key = classDef.ClassKey,
                        Value = MyStringId.GetOrCompute(classDef.ClassName)
                    });
                }
            };
            SelectClassDropdown.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(SelectClassDropdown);
            #endregion
        }

        static void CreateActions(IMyModContext context)
        {
            // Me When No Actions [https://imgur.com/a/hEMMxeR]
        }

        static ClassBeacon GetLogic(IMyTerminalBlock block) => block?.GameLogic?.GetAs<ClassBeacon>();
    }
}
