using Stride.Animations;
using Stride.Engine;
using Stride.Engine.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathWithSteer.Gameplay
{
    public class Interactable : ScriptComponent
    {
        public AnimationClip InteractionAnimation { get; set; }
        public TransformComponent interactPoint { get; set; }
        public EventKey OnInteract = new EventKey();
    }
}
