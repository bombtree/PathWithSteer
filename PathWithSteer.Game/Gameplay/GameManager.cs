using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Core.Mathematics;
using Stride.Input;
using Stride.Engine;
using Stride.Navigation;

namespace PathWithSteer.Gameplay
{
    public class GameManager : SyncScript
    {

        private DynamicNavigationMeshSystem dynamicNavigationMeshSystem;

        public override void Start()
        {
            base.Start();
            // Initialization of the script.
            dynamicNavigationMeshSystem = Game.GameSystems.OfType<DynamicNavigationMeshSystem>().FirstOrDefault();
        }

        public override void Update()
        {
        }
    }
}
