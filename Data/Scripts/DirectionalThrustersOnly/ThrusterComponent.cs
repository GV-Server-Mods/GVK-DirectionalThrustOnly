using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.Engine.Utils;
using VRage.Utils;

namespace DirectionalThrustersOnly
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class DirectionalThrustersOnlyEntityComponent : MyGameLogicComponent
    {
        private const double radiansToDegrees = 180 / Math.PI;

        private IMyThrust thruster;
        private IMyCubeGrid cubeGrid;
        private Vector3 thrusterDirection;
        private DirectionalThrustersOnlyConfiguration globalConfig;
        private DirectionalThrustersOnlyConfigurationItem thrusterConfig;
        private int tiltDetectionFrame;
        private int recoveryStartFrame;
        private bool isInTiltedState = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Session.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            thruster = (IMyThrust)Entity;
            cubeGrid = thruster.CubeGrid;
            thrusterDirection = Base6Directions.GetVector(thruster.Orientation.Forward);
            thruster.OnMarkForClose += OnMarkForClose;
        }

        public override void UpdateOnceBeforeFrame()
        {
            var config = DirectionalThrustersOnlySessionComponent.Instance?.Config;
            if (config == null || !Util.IsValid(cubeGrid) || DirectionalThrustersOnlySessionComponent.Instance.IsGridNPCOwned(cubeGrid))
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            var configForType = config.GetConfigForType(thruster.BlockDefinition);

            if (configForType == null)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            globalConfig = config;
            thrusterConfig = configForType;

            // Failsafe-ish action in case this is the first reconstructed thruster on a grid after losing all thrusters in a tilted direction
            var tiltedDirections = DirectionalThrustersOnlySessionComponent.Instance.GetTiltedDirections(cubeGrid);
            foreach (var direction in tiltedDirections)
            {
                if (direction.Direction == thrusterDirection)
                {
                    if (MyAPIGateway.Session.GameplayFrameCounter >= recoveryStartFrame)
                    {
                        DirectionalThrustersOnlySessionComponent.Instance.MarkDirectionRecovered(cubeGrid, thrusterDirection);
                    }
                }
            }
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (!Util.IsValid(cubeGrid))
            {
                return;
            }

            if (cubeGrid.Physics == null)
            {
                return;
            }

            var frame = MyAPIGateway.Session.GameplayFrameCounter;
            float naturalGravityInterference;
            var gravityNormal = MyAPIGateway.Physics.CalculateNaturalGravityAt(thruster.CubeGrid.PositionComp.GetPosition(), out naturalGravityInterference).Normalized();
            var gridOrientation = thruster.CubeGrid.PositionComp.GetOrientation();
            var blockThrustDirection = Vector3.TransformNormal(Base6Directions.GetVector(thruster.Orientation.Forward), gridOrientation);
            var dotProduct = blockThrustDirection.Dot(gravityNormal);
            var angleDegrees = (float)(radiansToDegrees * Math.Acos(dotProduct));

            if (angleDegrees < thrusterConfig.MaxAngleDegrees)
            {
                recoveryStartFrame = frame + globalConfig.recoveryTimeFrames;

                if (tiltDetectionFrame == 0)
                {
                    var tiltedDirections = DirectionalThrustersOnlySessionComponent.Instance.GetTiltedDirections(cubeGrid);

                    var earliestTilt = frame + globalConfig.tiltTimeFrames;
                    bool alreadyHasTilt = false;
                    foreach (var direction in tiltedDirections)
                    {
                        if (direction.TiltDetectedFrame <= earliestTilt)
                        {
                            earliestTilt = direction.TiltDetectedFrame;
                            if (direction.Direction == thrusterDirection)
                            {
                                alreadyHasTilt = true;
                            }
                            else
                            {
                                //MyLog.Default.WriteLineAndConsole($"DirectionalThrustersOnly: Found existing tilt direction for {cubeGrid.DisplayName} in direction {direction.Direction} (current:{thrusterDirection}) for frame {direction.TiltDetectedFrame} (now: {frame})");
                            }
                        }
                    }

                    tiltDetectionFrame = earliestTilt;

                    if (!alreadyHasTilt)
                    {
                        var tiltData = new TiltDirectionData()
                        {
                            Direction = thrusterDirection,
                            TiltDetectedFrame = tiltDetectionFrame,
                        };
                        DirectionalThrustersOnlySessionComponent.Instance.MarkDirectionTilted(cubeGrid, tiltData);
                    }
                }

                if (tiltDetectionFrame > 0 && frame >= tiltDetectionFrame)
                {
                    thruster.ThrustMultiplier = thrusterConfig.MinThrustMultiplier;
                    thruster.PowerConsumptionMultiplier = thrusterConfig.MinThrustMultiplier;
                    isInTiltedState = true;
                }
            }
            else
            {
                if (tiltDetectionFrame > 0 && frame < tiltDetectionFrame)
                {
                    tiltDetectionFrame = 0;
                    recoveryStartFrame = 0;
                    DirectionalThrustersOnlySessionComponent.Instance.MarkDirectionRecovered(cubeGrid, thrusterDirection);
                }
                else if (recoveryStartFrame > 0 && frame >= recoveryStartFrame)
                {
                    tiltDetectionFrame = 0;
                    recoveryStartFrame = 0;
                    if (isInTiltedState)
                    {
                        DirectionalThrustersOnlySessionComponent.Instance.MarkDirectionRecovered(cubeGrid, thrusterDirection);
                        thruster.ThrustMultiplier = 1f;
                        thruster.PowerConsumptionMultiplier = 1f;
                        isInTiltedState = false;
                    }
                }
            }
        }

        private void OnMarkForClose(IMyEntity entity)
        {
            DirectionalThrustersOnlySessionComponent.Instance.MarkDirectionRecovered(cubeGrid, thrusterDirection);
        }
    }
}