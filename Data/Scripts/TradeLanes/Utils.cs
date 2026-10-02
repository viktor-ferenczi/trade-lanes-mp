using System;
using System.IO;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using ProtoBuf;
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

// FOR LOOKUP
/*
namespace Digi.AttachedLights
{
    public static class Utils
    {
        /// <summary>
        /// Gets the flare definition for getting SubGlares into lights.
        /// NOTE: The subglare type is prohibited so I can't return that directly, which is why I'm returning the definition.
        /// </summary>
        public static MyFlareDefinition GetFlareDefinition(string flareSubtypeId)
        {
            if (string.IsNullOrEmpty(flareSubtypeId))
                throw new ArgumentException("flareSubtypeId must not be null or empty!");

            var flareDefId = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), flareSubtypeId);
            var flareDef = MyDefinitionManager.Static.GetDefinition(flareDefId) as MyFlareDefinition;

            if (flareDef == null)
                throw new Exception($"Couldn't find flare subtype {flareSubtypeId}");

            return flareDef;
        }

        /// <summary>
        /// Converts a relative mod path into a full path for local machine.
        /// NOTE: Do not start <paramref name="relativeTexturePath"/> with a slash!
        /// </summary>
        public static string GetModTextureFullPath(string relativeTexturePath)
        {
            return Path.Combine(AttachedLightsSession.Instance.ModContext.ModPath, relativeTexturePath);
        }
    }
}
*/

namespace Psycho.Utils
{
    public class Utils
    {
        public IMyShipController GetController(IMyCubeGrid grid)
        {
            if (grid == null)
                return null;

            // Get all ship controllers on the grid
            var shipControllers = GetShipControllersFromGrid(grid);

            // Check for a controller currently being controlled by a player or autopilot
            foreach (var controller in shipControllers)
            {
                //if (controller.IsUnderControl || controller.IsAutopilotEnabled)
                if (controller.IsUnderControl)
                {
                    return controller; // Return the active controller
                }
            }

            // Check for the main cockpit
            foreach (var controller in shipControllers)
            {
                if (controller.IsMainCockpit)
                {
                    return controller; // Return the main cockpit if found
                }
            }

            // Check for occupied cockpit
            foreach (var controller in shipControllers)
            {
                if (controller.Pilot != null)
                {
                    return controller; // Return the first available cockpit if no main cockpit is found
                }
            }

            // Fallback: Return null if no active controller is found
            return null;
            // Fallback: Return the first available controller
            return shipControllers.FirstOrDefault();
        }

        public List<IMyShipController> GetShipControllersFromGrid(IMyCubeGrid grid)
        {
            var shipControllers = new List<IMyShipController>();

            // Ensure the grid is valid  
            if (grid == null)
                return shipControllers;

            // Iterate through all blocks on the grid  
            var fatBlocks = grid.GetFatBlocks<IMyCubeBlock>();
            foreach (var block in fatBlocks)
            {
                IMyShipController controller = block as IMyShipController;
                // Check if the block is a ship controller
                if (controller != null)
                {
                    shipControllers.Add(controller);
                }
            }

            return shipControllers;
        }
    }
}