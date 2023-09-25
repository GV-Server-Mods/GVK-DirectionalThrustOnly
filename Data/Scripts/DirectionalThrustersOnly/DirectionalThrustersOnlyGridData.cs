using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.Game.Entities.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Utils;
using VRage.Game;
using Sandbox.Engine.Utils;

namespace DirectionalThrustersOnly
{
    public class TiltDirectionData
    {
        public Vector3 Direction;
        public int TiltDetectedFrame;
    }
}