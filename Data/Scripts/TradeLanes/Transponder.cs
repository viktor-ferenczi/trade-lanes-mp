using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using ProtoBuf;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;
using VRage.Game.Entity.UseObject;
using VRage.Network;
using Sandbox.Game.Localization;
using Sandbox.Game.World;
using VRage.Library.Utils;
//using static VRage.Audio.MyCueBank;
using VRage.Audio;
using VRage.Voxels;
using static VRage.Game.MyObjectBuilder_BehaviorTreeDecoratorNode;

using System.IO;
using System.Xml.Serialization;
using System.Runtime.Remoting.Messaging;
using static VRage.Game.MyObjectBuilder_Checkpoint;
using VRage.ObjectBuilders.Voxels;
using System.Net;
using System.Net.Sockets;
using ParallelTasks;
using static VRage.Game.MyObjectBuilder_CurveDefinition;
using ObjectBuilders.SafeZone;
using Sandbox.Game.WorldEnvironment.ObjectBuilders;
using Sandbox.Engine.Platform;
using Sandbox.Game.Lights;
using VRageRender.Lights;
using VRage;
using VRage.Game.Entities;
using static VRage.Game.MyObjectBuilder_SessionComponentMission;
using SpaceEngineers.Game.Entities.Blocks.SafeZone;
using VRage.Game.ObjectBuilders.Components;
using Sandbox.Game.Entities.Cube;
using System.Net.NetworkInformation;
using System.Net.Http.Headers;
using System.Security.Cryptography;
//using Microsoft.Xml.Serialization.GeneratedAssembly;
//using static System.Collections.Specialized.BitVector32;

namespace Psycho.TradeLanes
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TransponderBlock), false)]
    public class CustomTransponderLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock TerminalBlock;
        private IMyTransponder TransponderBlock;
        private bool Debug = false;
        static bool ControlsAdded = false;

        private const int CancelChannel = 100;
        private const int DockChannel = 101;
        private const int FormationChannel = 102;
        private const int WaypointChannel = 103;

        public bool Dock = false;
        public bool Formation = false;
        public bool Waypoint = false;

        private bool Enabled = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!Enabled)   
                return;

            if (Debug)
                MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "Init");

            TerminalBlock = Entity as IMyTerminalBlock;

            if (TerminalBlock != null)
            {
                NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            }

            if (Debug)
                MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "INITIALIZED");
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (Debug)
                MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "UpdateOnceBeforeFrame");

            TransponderBlock = Entity as IMyTransponder;

            if (TransponderBlock != null)
            {
                if (Debug)
                    MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "PREP FOR CONTROLS");
                // Add terminal controls
                if (!ControlsAdded && MyAPIGateway.TerminalControls != null)
                {
                    if (Debug)
                        MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "SETUP CONTROLS");
                    //AddDockButton();
                    //AddCancelDockButton();
                    AddToolbarActions();
                    ControlsAdded = true;
                }

                if (Debug)
                    MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "Subscribing to events");
                TransponderBlock.SignalReceived += OnSignalReceived;
                //TransponderBlock.TriggeredBySignal += OnSignalReceived2;
            }
        }

        public override void Close()
        {
            if (!Enabled)
                return;

            if (TransponderBlock != null)
            {
                TransponderBlock.SignalReceived -= OnSignalReceived;
                //TransponderBlock.TriggeredBySignal -= OnSignalReceived2;
            }
        }

        private void OnSignalReceived(int channel)
        {
            if (TransponderBlock?.CubeGrid?.Physics == null) return;

            switch (channel)
            {
                case DockChannel: // Dock signal
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived", "Docking signal received.!!");
                    break;
                case CancelChannel: // Cancel signal (unused)
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived", "Cancel docking signal received.!!");
                    break;
                default:
                    //MyAPIGateway.Utilities.ShowMessage("OnSignalReceived", $"Signal received: {channel}");
                    break;
            }
        }

        private void OnSignalReceived2(int channel)
        {
            if (TransponderBlock?.CubeGrid?.Physics == null) return;

            switch (channel)
            {
                case 98: // Dock signal
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived2", "Docking signal received.");
                    // Add your docking logic here
                    break;
                case 99: // Cancel dock signal
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived2", "Cancel docking signal received.");
                    // Add your cancel docking logic here
                    break;
                case 101: // Dock signal
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived2", "Docking signal received.!!");
                    // Add your docking logic here
                    break;
                case 102: // Cancel dock signal
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived2", "Cancel docking signal received.!!");
                    // Add your cancel docking logic here
                    break;
                default:
                    MyAPIGateway.Utilities.ShowMessage("OnSignalReceived2", $"Signal received: {channel}");
                    break;
            }
        }

        private void AddDockButton()
        {
            if (Debug)
                MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "DOCK CONTROLS");
            var dockButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("DockButton");
            dockButton.Title = MyStringId.GetOrCompute("Dock");
            dockButton.Tooltip = MyStringId.GetOrCompute("Initiates docking.");
            dockButton.Action = DockAction;
            dockButton.SupportsMultipleBlocks = false;
            //dockButton.Visible = (block) => block.BlockDefinition.SubtypeId == "TradeLaneTransponder";
            dockButton.Visible = (block) => block.BlockDefinition.SubtypeId.ToLower().Contains("transponder");
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(dockButton);
        }

        private void AddCancelDockButton()
        {
            if (Debug)
                MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "CANCEL CONTROLS");
            var cancelDockButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("CancelDockButton");
            cancelDockButton.Title = MyStringId.GetOrCompute("Cancel Dock");
            cancelDockButton.Tooltip = MyStringId.GetOrCompute("Cancels docking.");
            cancelDockButton.Action = CancelAction;
            cancelDockButton.SupportsMultipleBlocks = false;
            //cancelDockButton.Visible = (block) => block.BlockDefinition.SubtypeId == "TradeLaneTransponder";
            //cancelDockButton.Visible = (block) => block.BlockDefinition.SubtypeId.ToLower().Contains("transponder");
            cancelDockButton.Visible = (block) => block?.GameLogic?.GetAs<CustomTransponderLogic>() != null;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(cancelDockButton);
        }

        /*
        static void AddToolbarActions()
        {
            if (Debug)
                MyAPIGateway.Utilities.ShowMessage("TradeLane Transponder", "TOOLBAR CONTROLS");
            var dockAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("DockAction");
            dockAction.Name = new StringBuilder("Dock");
            dockAction.Icon = "Textures\\GUI\\Icons\\Actions\\Start.dds"; // Optional: Set an icon
            dockAction.Action = DockAction;
            dockAction.Writer = (block, builder) => builder.Append("Dock");
            MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(dockAction);

            var cancelDockAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("CancelDockAction");
            cancelDockAction.Name = new StringBuilder("Cancel Dock");
            cancelDockAction.Icon = "Textures\\GUI\\Icons\\Actions\\Stop.dds"; // Optional: Set an icon
            cancelDockAction.Action = CancelDockAction;
            cancelDockAction.Writer = (block, builder) => builder.Append("Cancel Dock");
            MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(cancelDockAction);

        }
        */

        static void AddToolbarActions()
        {
            // yes, there's only one type of action
            //var a = MyAPIGateway.TerminalControls.CreateAction<CustomTransponderLogic>("Dock");
            var a = MyAPIGateway.TerminalControls.CreateAction<IMyTransponder>("Dock");

            a.Name = new StringBuilder("Dock");

            // If the action is visible for grouped blocks (as long as they all have this action).
            a.ValidForGroups = false;

            // The icon shown in the list and top-right of the block icon in toolbar.
            a.Icon = @"Textures\GUI\Icons\Actions\SendSignal.dds";
            // For paths inside the mod folder you need to supply an absolute path which can be retrieved from a session or gamelogic comp's ModContext.
            //a.Icon = Path.Combine(context.ModPath, @"Textures\YourIcon.dds");

            // Called when the toolbar slot is triggered
            // Should not be unassigned.
            //a.Action = (b) => { };
            a.Action = (b) =>
            {
                /*
                var logic = b?.GameLogic?.GetAs<CustomTransponderLogic>();
                if (logic != null)
                {
                    logic.DockAction(b);
                }
                */
                var tb = b as IMyTransponder;
                tb.SendSignal(DockChannel);
            };

            // The status of the action, shown in toolbar icon text and can also be read by mods or PBs.
            a.Writer = (b, sb) =>
            {
                sb.Append("Hi\nthere");
            };

            // What toolbar types to NOT allow this action for.
            // Can be left unassigned to allow all toolbar types.
            // The below are the options used by jumpdrive's Jump action as an example.
            //a.InvalidToolbarTypes = new List<MyToolbarType>()
            //{
            //    MyToolbarType.ButtonPanel,
            //    MyToolbarType.Character,
            //    MyToolbarType.Seat
            //};
            // PB checks if it's valid for ButtonPanel before allowing the action to be invoked.

            // Wether the action is to be visible for the given block instance.
            // Can be left unassigned as it defaults to true.
            // Warning: gets called per tick while in toolbar for each block there, including each block in groups.
            //   It also can be called by mods or PBs.
            //a.Enabled = CustomVisibleCondition;

            MyAPIGateway.TerminalControls.AddAction<IMyTransponder>(a);
        }

        static bool CustomVisibleCondition(IMyTerminalBlock b)
        {
            // only visible for the blocks having this gamelogic comp
            return b?.GameLogic?.GetAs<CustomTransponderLogic>() != null;
        }

        void DockAction(IMyTerminalBlock block)
        {
            if (block == null) return;



            //MyAPIGateway.Utilities.ShowMessage("Dock", "Docking initiated.");
            Dock = !Dock;

            if (Dock)
                MyAPIGateway.Utilities.ShowNotification("Dock");
            else
                MyAPIGateway.Utilities.ShowNotification("Cancel Dock");
            //TransponderBlock.SendSignal(101);
            // Add your docking logic here
        }

        void CancelAction(IMyTerminalBlock block)
        {
            if (block == null) return;

            //MyAPIGateway.Utilities.ShowMessage("Cancel Dock", "Docking canceled.");
            //MyAPIGateway.Utilities.ShowNotification("Cancel");
            //TransponderBlock.SendSignal(102);
            // Add your cancel docking logic here
        }

        /*
        event Action<int> IMyTransponder.TriggeredBySignal
        {
            add
            {
                if (base.Components.TryGet(out IMySignalReceiverEntityComponent component))
                {
                    component.TriggeredBySignal += value;
                }
            }
        */
    }
}
