using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

// Multiplayer support.
//
// The server (or the single player game) is authoritative: it pairs the Trade Lane
// Computers, runs the docking sequence and moves the ships. Clients only draw the
// rings and play the effects. What they cannot work out on their own comes from
// the server through this channel:
//
// - LaneInfo: where a computer's lane leads. A client usually has only the near
//   computer streamed in, so it cannot pair them itself.
// - ShipSystem: a state change of a ship in a lane, for its sounds and particles.
// - GateEffect: particles on a ring when a ship requests docking or cancels it.
// - Notification: HUD text and chat lines for the players aboard the ship.

namespace Psycho.TradeLanes
{
    public enum MessageKind
    {
        LaneInfo,
        LaneInfoRequest,
        ShipSystem,
        GateEffect,
        Notification,
    }

    public enum GateEffectKind
    {
        RequestDocking,
        CancelDocking,
        StageChange,
    }

    [ProtoContract]
    public class TradeLaneMessage
    {
        [ProtoMember(1)]
        public MessageKind Kind;

        [ProtoMember(2)]
        public long EntityId;

        [ProtoMember(3)]
        public long OtherId;

        [ProtoMember(4)]
        public int Number;

        [ProtoMember(5)]
        public int Index;

        [ProtoMember(6)]
        public bool Flag;

        [ProtoMember(7)]
        public string Text;

        [ProtoMember(8)]
        public string Sender;

        [ProtoMember(9)]
        public double[] Vectors;
    }

    public static class TradeLaneNetwork
    {
        const ushort Channel = 34701;

        // A ship's status line repeats every frame, it goes out this often
        const int StatusInterval = 6;

        // and stays on the client's HUD this long after the last one
        const int StatusLifetimeMs = 300;

        static readonly Dictionary<long, int> StatusSentAt = new Dictionary<long, int>();
        static readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        static int Frame;
        static string StatusText;
        static int StatusUntil;

        public static bool IsServer => MyAPIGateway.Session.IsServer;

        static bool IsMultiplayer => MyAPIGateway.Multiplayer.MultiplayerActive;

        public static void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(Channel, OnMessage);
        }

        public static void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(Channel, OnMessage);
            StatusSentAt.Clear();
            StatusText = null;
        }

        // Called every frame from the session component
        public static void Update()
        {
            Frame++;
            if (StatusText != null)
            {
                if (Frame * 1000 / 60 < StatusUntil)
                    MyAPIGateway.Utilities.ShowNotification(StatusText, 16);
                else
                    StatusText = null;
            }
        }

        #region Server side

        public static void SendLaneInfo(
            long computerId,
            bool hasTarget,
            bool inherit,
            Vector3D target,
            Vector3D targetGrid,
            Vector3D targetUp,
            ulong to = 0
        )
        {
            if (!IsServer || !IsMultiplayer)
                return;

            var message = new TradeLaneMessage
            {
                Kind = MessageKind.LaneInfo,
                EntityId = computerId,
                Flag = hasTarget,
                Number = inherit ? 1 : 0,
                Vectors = new[]
                {
                    target.X,
                    target.Y,
                    target.Z,
                    targetGrid.X,
                    targetGrid.Y,
                    targetGrid.Z,
                    targetUp.X,
                    targetUp.Y,
                    targetUp.Z,
                },
            };
            Send(message, to);
        }

        public static void SendShipSystem(IMyCubeGrid grid, CustomGridLogic.ShipSystem state)
        {
            if (!IsServer || !IsMultiplayer)
                return;

            Send(
                new TradeLaneMessage
                {
                    Kind = MessageKind.ShipSystem,
                    EntityId = grid.EntityId,
                    Number = (int)state,
                }
            );
        }

        public static void SendGateEffect(
            long computerId,
            int gateIndex,
            IMyCubeGrid grid,
            GateEffectKind kind
        )
        {
            if (!IsServer || !IsMultiplayer)
                return;

            Send(
                new TradeLaneMessage
                {
                    Kind = MessageKind.GateEffect,
                    EntityId = computerId,
                    OtherId = grid?.EntityId ?? 0,
                    Index = gateIndex,
                    Number = (int)kind,
                }
            );
        }

        // Shows a HUD notification, or a chat line when sender is given, to the
        // players aboard the grid. A 16 ms notification is a status line the caller
        // repeats every frame.
        public static void Notify(
            IMyCubeGrid grid,
            string text,
            int ms = 2000,
            string sender = null
        )
        {
            if (!IsMultiplayer)
            {
                // Single player: everything shows, as it always did
                ShowLocal(text, ms, sender);
                return;
            }

            if (grid == null)
                return;

            bool status = sender == null && ms <= 16;
            bool send = true;
            if (status)
            {
                int last;
                if (
                    StatusSentAt.TryGetValue(grid.EntityId, out last)
                    && Frame - last < StatusInterval
                )
                    send = false;
                else
                    StatusSentAt[grid.EntityId] = Frame;
            }

            Players.Clear();
            MyAPIGateway.Players.GetPlayers(Players, player => IsAboard(player, grid));
            foreach (var player in Players)
            {
                if (player == MyAPIGateway.Session.Player)
                {
                    ShowLocal(text, ms, sender);
                    continue;
                }

                if (!send)
                    continue;

                Send(
                    new TradeLaneMessage
                    {
                        Kind = MessageKind.Notification,
                        Text = text,
                        Number = status ? StatusLifetimeMs : ms,
                        Flag = status,
                        Sender = sender,
                    },
                    player.SteamUserId
                );
            }
            Players.Clear();
        }

        static bool IsAboard(IMyPlayer player, IMyCubeGrid grid)
        {
            var controlled = player.Controller?.ControlledEntity?.Entity as IMyCubeBlock;
            if (controlled?.CubeGrid == grid)
                return true;

            var seat = player.Character?.Parent as IMyCubeBlock;
            return seat?.CubeGrid == grid;
        }

        static void Send(TradeLaneMessage message, ulong to = 0)
        {
            var data = MyAPIGateway.Utilities.SerializeToBinary(message);
            if (to == 0)
                MyAPIGateway.Multiplayer.SendMessageToOthers(Channel, data);
            else
                MyAPIGateway.Multiplayer.SendMessageTo(Channel, data, to);
        }

        #endregion

        #region Client side

        // Asks the server where a computer's lane leads, the answer is a LaneInfo
        public static void RequestLaneInfo(long computerId)
        {
            if (IsServer)
                return;

            var data = MyAPIGateway.Utilities.SerializeToBinary(
                new TradeLaneMessage { Kind = MessageKind.LaneInfoRequest, EntityId = computerId }
            );
            MyAPIGateway.Multiplayer.SendMessageToServer(Channel, data);
        }

        static void ShowLocal(string text, int ms, string sender)
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            if (sender != null)
                MyAPIGateway.Utilities.ShowMessage(sender, text);
            else
                MyAPIGateway.Utilities.ShowNotification(text, ms);
        }

        #endregion

        static void OnMessage(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            try
            {
                var message = MyAPIGateway.Utilities.SerializeFromBinary<TradeLaneMessage>(data);
                if (message == null)
                    return;

                if (message.Kind == MessageKind.LaneInfoRequest)
                {
                    if (IsServer)
                        Computer(message.EntityId)?.SendLaneInfo(sender);
                    return;
                }

                // Everything else is the server's word
                if (!fromServer || IsServer)
                    return;

                switch (message.Kind)
                {
                    case MessageKind.LaneInfo:
                        var v = message.Vectors;
                        if (v == null || v.Length < 9)
                            return;
                        Computer(message.EntityId)
                            ?.SetLaneInfo(
                                message.Flag,
                                message.Number != 0,
                                new Vector3D(v[0], v[1], v[2]),
                                new Vector3D(v[3], v[4], v[5]),
                                new Vector3D(v[6], v[7], v[8])
                            );
                        break;

                    case MessageKind.ShipSystem:
                        var grid = Entity(message.EntityId) as MyCubeGrid;
                        grid?.GameLogic?.GetAs<CustomGridLogic>()
                            ?.ExecShipSystems((CustomGridLogic.ShipSystem)message.Number);
                        break;

                    case MessageKind.GateEffect:
                        Computer(message.EntityId)
                            ?.GateEffect(
                                message.Index,
                                Entity(message.OtherId) as MyCubeGrid,
                                (GateEffectKind)message.Number
                            );
                        break;

                    case MessageKind.Notification:
                        if (message.Flag)
                        {
                            StatusText = message.Text;
                            StatusUntil = Frame * 1000 / 60 + message.Number;
                        }
                        else
                        {
                            ShowLocal(message.Text, message.Number, message.Sender);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("TRADE LANES Network", e.Message);
            }
        }

        static IMyEntity Entity(long id)
        {
            IMyEntity entity;
            return id != 0 && MyAPIGateway.Entities.TryGetEntityById(id, out entity)
                ? entity
                : null;
        }

        static TradeLaneComputerBlockLogic Computer(long id)
        {
            return (
                Entity(id) as IMyTerminalBlock
            )?.GameLogic?.GetAs<TradeLaneComputerBlockLogic>();
        }
    }
}
