using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Core.Mathematics;
using Stride.Input;
using Stride.Engine;

namespace PathWithSteer.Core
{
    public class EnableCollisionDebug : SyncScript
    {
        // Declared public member fields and properties will show in the game studio
        private PhysicsComponent PhysicsComponent { get; set; }
        public override void Start()
        {
            // Initialization of the script.
            PhysicsComponent = Entity.Components.Get<PhysicsComponent>();
            PhysicsComponent.AddDebugEntity(Entity.Scene);
        }

        public override void Update()
        {
        }

        public override void Cancel()
        {
            base.Cancel();
            if(Entity.Scene != null )
            {
                PhysicsComponent.RemoveDebugEntity(Entity.Scene);
            }
        }
    }
}
