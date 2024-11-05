using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.Components;
using static Scripts.Structure;

namespace Scripts
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate, int.MaxValue)]
    public class Communication : MySessionComponentBase
    {
        internal byte[] Storage;

        #region Overrides
        public override void LoadData()
        {
            Log.Init($"{ModContext.ModName}-Init.log");
            MyAPIGateway.Utilities.RegisterMessageHandler(6831, Handler);
            Init();
            SendModMessage(true);
        }

        protected override void UnloadData()
        {
            Log.Close();
            MyAPIGateway.Utilities.UnregisterMessageHandler(6831, Handler);
            Array.Clear(Storage, 0, Storage.Length);
            Storage = null;
        }
        #endregion

        #region Event Handlers
        void Handler(object o)
        {
            if (o == null) SendModMessage(false);
        }

        void SendModMessage(bool sending)
        {
            Log.CleanLine(sending ? "Sending Request to Core Module" : "Receiving Request to Core Module");
            MyAPIGateway.Utilities.SendModMessage(6831, Storage);
        }
        #endregion

        internal void Init()
        {
            ContainerDefinition baseDefs;
            Parts.GetBaseDefinitions(out baseDefs);
            Parts.SetModPath(baseDefs, ModContext.ModPath);
            Storage = MyAPIGateway.Utilities.SerializeToBinary(baseDefs);
            Log.CleanLine($"Initializing...");
        }

        #region Logger
        public class Log
        {
            private static Log _instance = null;
            internal TextWriter File = null;

            public static void Init(string name)
            {
                var sb = new StringBuilder(name);
                ReplaceAll(sb, Path.GetInvalidFileNameChars(), '_');
                _instance = new Log { File = MyAPIGateway.Utilities.WriteFileInLocalStorage(sb.ToString(), typeof(Log)) };
            }

            public static void ReplaceAll(StringBuilder sb, char[] charlist, char replacewith)
            {
                for (int i = 0; i < sb.Length; i++)
                {
                    if (charlist.Contains(sb[i]))
                        sb[i] = replacewith;
                }
            }

            public static void CleanLine(string text)
            {
                _instance.File.WriteLine(text);
                _instance.File.Flush();
            }

            public static void Close()
            {
                if (_instance?.File == null) return;
                _instance.File.Flush();
                _instance.File.Close();
                _instance.File.Dispose();
                _instance.File = null;
                _instance = null;
            }
        }
        #endregion
    }
}