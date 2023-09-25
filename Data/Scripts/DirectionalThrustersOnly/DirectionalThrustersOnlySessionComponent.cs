using System;
using System.Collections;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DirectionalThrustersOnly
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DirectionalThrustersOnlySessionComponent : MySessionComponentBase
    {
        public static DirectionalThrustersOnlySessionComponent Instance { get; private set; }
        public DirectionalThrustersOnlyConfiguration Config { get; private set; }
        public Dictionary<IMyCubeGrid, List<TiltDirectionData>> TiltedGrids { get; private set; } = new Dictionary<IMyCubeGrid, List<TiltDirectionData>>();

        private static readonly List<TiltDirectionData> EmptyTiltList = new List<TiltDirectionData>(0);

        private readonly List<long> npcFactionMembers = new List<long>();

        public override void LoadData()
        {
            if (MyAPIGateway.Session.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                Config = DirectionalThrustersOnlyConfiguration.LoadSettings();
            }

            Instance = this;
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdded;

            foreach (var faction in MyAPIGateway.Session.Factions.Factions.Values)
            {
                if (!faction.IsEveryoneNpc())
                {
                    continue;
                }

                foreach (var memberId in faction.Members.Keys)
                {
                    npcFactionMembers.Add(memberId);
                }
            }

        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdded;
            TiltedGrids.Clear();
            Instance = null;
        }

        private void OnEntityAdded(IMyEntity entity)
        {
            if (entity is IMyCubeGrid)
            {
                OnGridAdded((IMyCubeGrid)entity);
            }
        }

        private void OnGridAdded(IMyCubeGrid grid)
        {
            if (!Util.IsValid(grid)) { return; }
            grid.OnIsStaticChanged += OnIsStaticChanged;
            if (!grid.IsStatic && grid.Physics != null)
            {
                MyLog.Default.WriteLineAndConsole($"DirectionalThrustersOnly: Started to track grid {grid.DisplayName}");
                grid.OnMarkForClose += OnGridMarkedForClose;
            }
        }

        private void OnIsStaticChanged(IMyCubeGrid grid, bool newIsStatic)
        {
            if (!Util.IsValid(grid)) { return; }

            if (newIsStatic)
            {
                MyLog.Default.WriteLineAndConsole($"DirectionalThrustersOnly: Stopped tracking grid {grid.DisplayName} because the grid is static");
                StopTrackingGrid(grid);
            }
        }

        private void OnGridMarkedForClose(IMyEntity entity)
        {
            var grid = entity as IMyCubeGrid;
            if (entity != null)
            {
                MyLog.Default.WriteLineAndConsole($"DirectionalThrustersOnly: Stopped tracking grid {grid.DisplayName} because it is marked for close");
                StopTrackingGrid(grid);
            }
        }

        private void StopTrackingGrid(IMyCubeGrid grid)
        {
            TiltedGrids.Remove(grid);
        }

        internal List<TiltDirectionData> GetTiltedDirections(IMyCubeGrid cubeGrid)
        {
            List<TiltDirectionData> tiltData;
            if (TiltedGrids.TryGetValue(cubeGrid, out tiltData))
            {
                return tiltData;
            }
            return EmptyTiltList;
        }

        internal void MarkDirectionTilted(IMyCubeGrid cubeGrid, TiltDirectionData directionData)
        {
            if (Util.IsValid(cubeGrid))
            {
                MyLog.Default.WriteLineAndConsole($"DirectionalThrustersOnly: Tilting grid {cubeGrid.DisplayName} in direction {directionData.Direction} with detection at {directionData.TiltDetectedFrame}");
            }
            List<TiltDirectionData> tiltData;
            if (TiltedGrids.TryGetValue(cubeGrid, out tiltData))
            {
                foreach (var item in tiltData)
                {
                    if (item.Direction == directionData.Direction)
                    {
                        if (directionData.TiltDetectedFrame > item.TiltDetectedFrame)
                        {
                            item.TiltDetectedFrame = directionData.TiltDetectedFrame;
                        }
                        return;
                    }
                }
            }
            else
            {
                tiltData = new List<TiltDirectionData>(6);
                TiltedGrids.Add(cubeGrid, tiltData);
            }
            tiltData.Add(directionData);
        }

        internal void MarkDirectionRecovered(IMyCubeGrid cubeGrid, Vector3 direction)
        {
            List<TiltDirectionData> tiltData;
            if (TiltedGrids.TryGetValue(cubeGrid, out tiltData))
            {
                if (Util.IsValid(cubeGrid))
                {
                    MyLog.Default.WriteLineAndConsole($"DirectionalThrustersOnly: Recovering grid {cubeGrid.DisplayName} in direction {direction}");
                }
                if (tiltData.Count == 0)
                {
                    TiltedGrids.Remove(cubeGrid);
                    return;
                }
                if (tiltData.Count == 1 && tiltData[0].Direction == direction)
                {
                    TiltedGrids.Remove(cubeGrid);
                    return;
                }
                for (int i = tiltData.Count - 1; i >= 0; i--)
                {
                    if (tiltData[i].Direction == direction)
                    {
                        tiltData.RemoveAtFast(i);
                        return;
                    }
                }
            }
        }

        internal bool IsGridNPCOwned(IMyCubeGrid cubeGrid)
        {
            foreach (var owner in cubeGrid.BigOwners)
            {
                if (npcFactionMembers.Contains(owner))
                {
                    return true;
                }
            }
            return false;
        }
    }
}