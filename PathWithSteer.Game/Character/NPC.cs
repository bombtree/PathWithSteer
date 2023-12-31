using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Core.Mathematics;
using Stride.Input;
using Stride.Engine;
using PathWithSteer.Core;
using Stride.Physics;
using Stride.Navigation;
using Stride.Core;
using Stride.Particles.Components;
using Stride.Particles.Initializers;

namespace PathWithSteer.Character
{
    public class NPC : CharacterController
    {
        private bool isRunning = false;
        private int destinationIndex = 0;
        private Vector3 moveDirection = Vector3.Zero;
        private Entity modelChildEntity;

        private Vector3 moveDestination;

        private Vector3 lastVelocity = Vector3.Zero;
        private List<Vector3> pathToDestination = new List<Vector3>();

        /// <summary>
        /// The maximum speed the character can run at
        /// </summary>
        [Display("Run Speed")]
        public float MaxRunSpeed { get; set; } = 10;

        public float CurrentRunSpeed { get; set; } = 3.0f;
        public NavigationComponent Navigation { get; set; }
        public float DestinationSlowdown { get; set; } = 0.4f;
        public float DestinationThreshold { get; set; } = 0.2f;
        public float CornerSlowdown { get; set; } = 0.6f;
        public ParticleSystemComponent TestRayParticleSystem { get; set; }



        // Declared public member fields and properties will show in the game studio
        public List<Entity> WalkDestinations = new List<Entity>();

        public override void Start()
        {
            base.Start();
            Log.ActivateLog(Stride.Core.Diagnostics.LogMessageType.Debug);
            character = Entity.Get<CharacterComponent>();
            steering = new Steering(character, Log);
            steering.TestRayParticleSystem = TestRayParticleSystem;
            foreach (var initializer in TestRayParticleSystem.ParticleSystem.Emitters.First().Initializers)
            {
                var positionArc = initializer as InitialPositionArc;
                if (positionArc != null)
                {
                    steering.TestRayEndTransform = positionArc.Target;
                }
            }
            modelChildEntity = Entity.GetChild(0);
            WalkDestinations.Shuffle(new Random(this.GetHashCode()));
            destinationIndex = -1;
            SetNextDestination();
        }
        bool doMove = true;
        public override void Update()
        {
            // Do stuff every new frame
            if (Input.IsKeyPressed(Keys.Space))
            {
                doMove = !doMove;
                Log.Debug(doMove ? "Moving" : "Seeking");
            }
            
            Move(CurrentRunSpeed);
            if (steering.ReachedDestination)
            {
                SetNextDestination();
            }
        }

        private void Move(float speed)
        {
            UpdateMoveTowardsDestination(speed);
        }
        private void UpdateMoveTowardsDestination(float speed)
        {
            Vector3 moveVelocity;
            steering.CurrentMaxVelocity = speed;
            //moveVelocity = steering.Evade(PersuitTarget, Entity.Transform.WorldMatrix.TranslationVector, currentVelocity);
            moveVelocity = steering.Steer();

            character.SetVelocity(moveVelocity);
            // Broadcast speed as percent of the max speed
            RunSpeedEventKey.Broadcast(moveVelocity.Length() / MaxRunSpeed);
            // Character orientation
            if (moveVelocity.Length() > 0.001f)
            {
                float yawOrientation;
                yawOrientation = MathF.Atan2(-moveVelocity.Z, moveVelocity.X) + MathUtil.PiOverTwo;
                modelChildEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(yawOrientation, 0, 0);
            }
            lastVelocity = moveVelocity;
        }

        private void SetNextDestination()
        {
            ++destinationIndex;
            if (destinationIndex == WalkDestinations.Count)
            {
                destinationIndex = 0;
                WalkDestinations.Shuffle(new Random((int)Game.TargetElapsedTime.TotalMilliseconds));
            }
            UpdateDestination(WalkDestinations[destinationIndex].Transform.WorldMatrix.TranslationVector);
        }

        NavigationQuerySettings querySettings = new NavigationQuerySettings()
        {
            FindNearestPolyExtent = new Vector3(3f, 4f, 3f),
            MaxPathPoints = NavigationQuerySettings.Default.MaxPathPoints
        };

        private void UpdateDestination(Vector3 destination)
        {
            if (/*delta.Length() > 0.01f*/true) // Only recalculate path when the target position is different
            {
                // Generate a new path using the navigation component
                pathToDestination.Clear();
                if (Navigation.TryFindPath(destination, pathToDestination))
                {
                    steering.CurrentPath = pathToDestination;
                }
                else
                {
                    // Could not find a path to the target location
                    pathToDestination.Clear();
                    HaltMovement();
                }
            }
        }
        private void HaltMovement()
        {
            moveDirection = Vector3.Zero;
            character.SetVelocity(Vector3.Zero);
            moveDestination = modelChildEntity.Transform.WorldMatrix.TranslationVector;
        }
    }

}
