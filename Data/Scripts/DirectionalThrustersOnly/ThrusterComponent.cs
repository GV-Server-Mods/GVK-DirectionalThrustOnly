using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DirectionalThrustersOnly
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class DirectionalThrustersOnlyEntityComponent : MyGameLogicComponent
    {

        private IMyThrust thruster;
        private Vector3 thrusterDirection;
        private DirectionalThrustersOnlyConfigurationItem config;
        private float upgradeThrustMultiplier = 1f;
        private float upgradePowerMultiplier = 1f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Session.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            thruster = (IMyThrust)Entity;
            thrusterDirection = Base6Directions.GetVector(thruster.Orientation.Forward);
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!Util.IsValid(thruster))
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            var instanceConfig = DirectionalThrustersOnlySessionComponent.Instance?.Config;
            if (instanceConfig == null)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            var cubeGrid = thruster.CubeGrid;

            if (!Util.IsValid(cubeGrid) || DirectionalThrustersOnlySessionComponent.Instance.IsGridNPCOwned(cubeGrid))
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            if (!instanceConfig.TryGetConfigForType(thruster.BlockDefinition, out config))
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            cubeGrid.OnIsStaticChanged += CubeGridOnIsStaticChanged;
            thruster.AddUpgradeValue("HandlesUpgrade", 1f);
            thruster.OnUpgradeValuesChanged += ThrusterOnUpgradeValuesChanged;

            NeedsUpdate = cubeGrid.IsStatic ? MyEntityUpdateEnum.NONE : MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void Close()
        {
            if (thruster != null)
            {
                thruster.OnUpgradeValuesChanged -= ThrusterOnUpgradeValuesChanged;
                if (thruster.CubeGrid != null)
                {
                    thruster.CubeGrid.OnIsStaticChanged -= CubeGridOnIsStaticChanged;
                }
            }
        }

        private void CubeGridOnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            NeedsUpdate = isStatic ? MyEntityUpdateEnum.NONE : MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        private void ThrusterOnUpgradeValuesChanged()
        {
            if (!Util.IsValid(thruster))
            {
                return;
            }

            if (!thruster.UpgradeValues.TryGetValue("ThrustMultiplier", out upgradeThrustMultiplier))
            {
                upgradeThrustMultiplier = 1f;
            }
            if (!thruster.UpgradeValues.TryGetValue("PowerConsumptionMultiplier", out upgradePowerMultiplier))
            {
                upgradePowerMultiplier = 1f;
            }
        }

        public override void UpdateBeforeSimulation10()
        {
            if (!Util.IsValid(thruster) || !thruster.IsWorking)
            {
                return;
            }

            var cubeGrid = thruster.CubeGrid;

            if (!Util.IsValid(cubeGrid) || cubeGrid.Physics == null)
            {
                return;
            }

            if (cubeGrid.Physics.Speed < 5f)
            {
                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            }

            SetMultipliers(cubeGrid);
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!Util.IsValid(thruster) || !thruster.IsWorking)
            {
                return;
            }

            var cubeGrid = thruster.CubeGrid;

            if (!Util.IsValid(cubeGrid) || cubeGrid.Physics == null)
            {
                return;
            }

            if (cubeGrid.Physics.Speed >= 5f)
            {
                NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
            }

            SetMultipliers(cubeGrid);
        }

        private void SetMultipliers(IMyCubeGrid cubeGrid)
        {
            var gravityVector = cubeGrid.NaturalGravity;
            if (MathHelper.IsZero(gravityVector))
            {
                thruster.ThrustMultiplier = 1 * upgradeThrustMultiplier;
                thruster.PowerConsumptionMultiplier = 1 * upgradePowerMultiplier;
                return;
            }

            var gravityNormal = gravityVector.Normalized();
            var gridOrientation = cubeGrid.PositionComp.GetOrientation();
            var worldThrustDirection = Vector3.TransformNormal(thrusterDirection, gridOrientation);
            var angleFromGravity = MathHelper.ToDegrees((float)Math.Acos(worldThrustDirection.Dot(gravityNormal)));

            if (angleFromGravity < config.MinThrustDegrees)
            {
                thruster.ThrustMultiplier = config.MinThrustMultiplier * upgradeThrustMultiplier;
                thruster.PowerConsumptionMultiplier = config.MinThrustMultiplier * upgradePowerMultiplier;
            }
            else if (angleFromGravity < config.FalloffStartDegrees)
            {
                var requestedThrustMultiplier = MathHelper.Clamp(MathHelper.Lerp(config.MinThrustMultiplier, 1, (angleFromGravity - config.MinThrustDegrees) / (config.FalloffStartDegrees - config.MinThrustDegrees)), 0.01f, 1);
                thruster.ThrustMultiplier = requestedThrustMultiplier * upgradeThrustMultiplier;
                thruster.PowerConsumptionMultiplier = requestedThrustMultiplier * upgradePowerMultiplier;
            }
            else
            {
                thruster.ThrustMultiplier = 1 * upgradeThrustMultiplier;
                thruster.PowerConsumptionMultiplier = 1 * upgradePowerMultiplier;
            }
        }
    }
}