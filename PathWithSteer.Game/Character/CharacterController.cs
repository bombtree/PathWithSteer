using PathWithSteer.Gameplay;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathWithSteer.Character
{
    public abstract class CharacterController : SyncScript
    {
        [DataMemberIgnore]
        public EventKey<float> RunSpeedEventKey { get; protected set; } = new EventKey<float>();
        
        [DataMemberIgnore]
        public EventKey<bool> IsAttackingEventKey { get; protected set; } = new EventKey<bool>();
        
        [DataMemberIgnore]
        public EventKey<Interactable> InteractableEventKey { get; protected set; } = new EventKey<Interactable>();

        [DataMemberIgnore]
        public Vector3 currentVelocity
        {
            get
            {
                //Velocity is multiplied by the fixed timestep for the physics update, so divide by that.
                return character.LinearVelocity / character.Simulation.FixedTimeStep;
            }
        }

        protected CharacterComponent character;

        protected Steering steering;
    }
}
