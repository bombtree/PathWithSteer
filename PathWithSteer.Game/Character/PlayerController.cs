// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Navigation;
using Stride.Physics;
using PathWithSteer.Core;
using System.Diagnostics;
using Stride.Particles.Updaters.FieldShapes;
using Stride.Rendering;
using Stride.Particles;
using System.Linq;
using Stride.Particles.Initializers;
using Stride.Particles.Components;
using Stride.Core.Collections;
using System.IO;
using PathWithSteer.Gameplay;
using PathWithSteer.Character;
using Stride.Core.Diagnostics;

namespace PathWithSteer.Player
{
    public class PlayerController : CharacterController
    {
        // The character controller does only two things - moves the character and makes it attack close targets
        //  If the character is too far from its target it will run after it until it's close enough then halt movement and attack
        //  If the character is walking towards a specific location instead it will run to it then halt movement when close enough

        private EventReceiver<ClickResult> moveDestinationEvent;

        private EventReceiver<Vector3> joystickLocalVectorEvent;

        /// <summary>
        /// The maximum speed the character can run at
        /// </summary>
        [Display("Run Speed")]
        public float MaxRunSpeed { get; set; } = 10;
        
        /// <summary>
        /// The distance from the destination at which the character will stop moving
        /// </summary>
        public float DestinationThreshold { get; set; } = 0.2f;

        /// <summary>
        /// A number from 0 to 1 indicating how much a character should slow down when going around corners
        /// </summary>
        /// <remarks>0 is no slowdown and 1 is completely stopping (on >90 degree angles)</remarks>
        public float CornerSlowdown { get; set; } = 0.6f;

        /// <summary>
        /// Multiplied by the distance to the target and clamped to 1 and used to slow down when nearing the destination
        /// </summary>
        public float DestinationSlowdown { get; set; } = 0.4f;

        
        // Allow some inertia to the movement
        private Vector3 moveDirection = Vector3.Zero;

        private bool isRunning = false;

        // Attacking
        [Display("Punch Collision")]
        public RigidbodyComponent PunchCollision { get; set; }

        /// <summary>
        /// The maximum distance from which the character can perform an attack
        /// </summary>
        [Display("Attack Distance")]
        public float AttackDistance { get; set; } = 1f;

        /// <summary>
        /// Cooldown in seconds required for the character to recover from starting an attack until it can choose another action
        /// </summary>
        [Display("Attack Cooldown")]
        public float AttackCooldown { get; set; } = 0.65f;

        public float MaxHoldToMoveDistance { get; set; } = 5.0f;

        public ParticleSystemComponent TestRayParticleSystem { get; set; }

        private Entity modelChildEntity;
        private float yawOrientation;

        private Entity attackEntity = null;
        private float attackCooldown = 0f;

        // Pathfinding Component
        private NavigationComponent navigation;
        private readonly List<Vector3> pathToDestination = new List<Vector3>();
        private int waypointIndex;
        private Vector3 moveDestination;

        private bool ReachedDestination => waypointIndex >= pathToDestination.Count;

        private Vector3 CurrentWaypoint => waypointIndex < pathToDestination.Count ? pathToDestination[waypointIndex] : Vector3.Zero;

        private TransformComponent testRayEndTransform;

        private static readonly NavigationQuerySettings holdToMoveQuerySettings = new NavigationQuerySettings()
        {
            FindNearestPolyExtent = NavigationQuerySettings.Default.FindNearestPolyExtent,
            MaxPathPoints = 3
        };

        /// <summary>
        /// Called when the script is first initialized
        /// </summary>
        public override void Start()
        {
            base.Start();
            Log.ActivateLog(Stride.Core.Diagnostics.LogMessageType.Debug);

            // Get the navigation component on the same entity as this script
            navigation = Entity.Get<NavigationComponent>();

            // Will search for an CharacterComponent within the same entity as this script
            character = Entity.Get<CharacterComponent>();
            if (character == null) throw new ArgumentException("Please add a CharacterComponent to the entity containing PlayerController!");

            if (PunchCollision == null) throw new ArgumentException("Please add a RigidbodyComponent as a PunchCollision to the entity containing PlayerController!");

            modelChildEntity = Entity.GetChild(0);

            moveDestination = Entity.Transform.WorldMatrix.TranslationVector;

            PunchCollision.Enabled = false;

            var playerInput = Entity.Components.Get<PlayerInput>();
            joystickLocalVectorEvent = new EventReceiver<Vector3>(playerInput.JoystickLocalVectorEventKey);
            moveDestinationEvent = new EventReceiver<ClickResult>(playerInput.MoveDestinationEventKey);

            foreach (var initializer in TestRayParticleSystem.ParticleSystem.Emitters.First().Initializers)
            {
                var positionArc = initializer as InitialPositionArc;
                if(positionArc != null)
                {
                    testRayEndTransform = positionArc.Target;
                }
            }
        }

        /// <summary>
        /// Called on every frame update
        /// </summary>
        public override void Update()
        {
            Attack();
            UpdateDestination(moveDestination);
            Move(MaxRunSpeed);
        }

        private void Attack()
        {
            var dt = (float) Game.UpdateTime.Elapsed.TotalSeconds;
            attackCooldown = (attackCooldown > 0) ? attackCooldown - dt : 0f;

            PunchCollision.Enabled = (attackCooldown > 0);

            var interactable = attackEntity?.Components.Get<Interactable>();

            if(interactable != null)
            {
                var directionToCharacter = interactable.interactPoint.WorldMatrix.TranslationVector -
                                           modelChildEntity.Transform.WorldMatrix.TranslationVector;
                directionToCharacter.Y = 0;

                var currentDistance = directionToCharacter.Length();
                if (currentDistance <= AttackDistance)
                {
                    // Attack!
                    HaltMovement();

                    attackEntity = null;
                    attackCooldown = AttackCooldown;
                    PunchCollision.Enabled = true;
                    IsAttackingEventKey.Broadcast(true);
                    InteractableEventKey.Broadcast(interactable);
                }
                else
                {
                    UpdateDestination(interactable.interactPoint.WorldMatrix.TranslationVector);
                }
            }
        }

        private void HaltMovement()
        {
            isRunning = false;
            moveDirection = Vector3.Zero;
            character.SetVelocity(Vector3.Zero);
            moveDestination = modelChildEntity.Transform.WorldMatrix.TranslationVector;
        }

        private void UpdateDestination(Vector3 destination)
        {
            Vector3 delta = moveDestination - destination;
            if (/*delta.Length() > 0.01f*/true) // Only recalculate path when the target position is different
            {
                // Generate a new path using the navigation component
                pathToDestination.Clear();
                if (navigation.TryFindPath(destination, pathToDestination))
                {
                    // Skip the points that are too close to the player
                    waypointIndex = 0;
                    while (!ReachedDestination && (CurrentWaypoint - Entity.Transform.WorldMatrix.TranslationVector).Length() < 0.25f)
                    {
                        waypointIndex++;
                    }

                    // If this path still contains more points, set the player to running
                    if (!ReachedDestination)
                    {
                        isRunning = true;
                        moveDestination = destination;
                    }
                }
                else
                {
                    // Could not find a path to the target location
                    pathToDestination.Clear();
                    HaltMovement();
                }
            }
        }

        private void UpdateMoveTowardsDestination(float speed, float joystickStrength)
        {
            if (!ReachedDestination)
            {
                var direction = CurrentWaypoint - Entity.Transform.WorldMatrix.TranslationVector;

                // Get distance towards next point and normalize the direction at the same time
                var length = direction.Length();
                direction /= length;

                // Check when to advance to the next waypoint
                bool advance = false;

                // Check to see if an intermediate point was passed by projecting the position along the path
                if (pathToDestination.Count > 0 && waypointIndex > 0 && waypointIndex != pathToDestination.Count - 1)
                {
                    Vector3 pointNormal = CurrentWaypoint - pathToDestination[waypointIndex-1];
                    pointNormal.Normalize();
                    float current = Vector3.Dot(Entity.Transform.WorldMatrix.TranslationVector, pointNormal);
                    float target = Vector3.Dot(CurrentWaypoint, pointNormal);
                    if (current > target)
                    {
                        advance = true;
                    }
                }
                else
                {
                    if (length < DestinationThreshold) // Check distance to final point
                    {
                        advance = true;
                    }
                }

                // Advance waypoint?
                if (advance)
                {
                    waypointIndex++;
                    if (ReachedDestination)
                    {
                        // Final waypoint reached
                        HaltMovement();
                        return;
                    }
                }

                // Calculate speed based on distance from final destination
                float speedScale = Math.Max((moveDestination - Entity.Transform.WorldMatrix.TranslationVector).Length(), joystickStrength / DestinationSlowdown) * DestinationSlowdown;
                if (speedScale > 1.0f)
                {
                    speedScale = 1.0f;
                }

                // Slow down around corners
                float cornerSpeedMultiply = Math.Max(0.0f, Vector3.Dot(direction, moveDirection)) * CornerSlowdown + (1.0f - CornerSlowdown);

                // Allow a very simple inertia to the character to make animation transitions more fluid
                moveDirection = moveDirection * 0.85f + direction * speedScale * cornerSpeedMultiply * 0.15f;

                character.SetVelocity(moveDirection * speed);

                // Broadcast speed as per cent of the max speed
                RunSpeedEventKey.Broadcast(moveDirection.Length());

                // Character orientation
                if (moveDirection.Length() > 0.001)
                {
                    yawOrientation = MathUtil.RadiansToDegrees((float) Math.Atan2(-moveDirection.Z, moveDirection.X) + MathUtil.PiOverTwo);
                }
                modelChildEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(MathUtil.DegreesToRadians(yawOrientation), 0, 0);
            }
            else
            {
                // No target
                HaltMovement();
            }
        }

        private void Move(float speed)
        {
            if (attackCooldown > 0)
                return;

            // Character speed
            ClickResult clickResult;
            float joystickStrength = 0.0f;
            if (moveDestinationEvent.TryReceive(out clickResult) && clickResult.Type != ClickType.Empty)
            {
                if (clickResult.Type == ClickType.Ground)
                {
                    attackEntity = null;
                    UpdateDestination(clickResult.WorldPosition);
                }

                if (clickResult.Type == ClickType.LootCrate)
                {
                    attackEntity = clickResult.ClickedEntity;
                    Attack();
                }
            }
            else if (joystickLocalVectorEvent.TryReceive(out Vector3 joyStickDirection))
            {
                Vector3 normalizedStickDir = Vector3.Normalize(joyStickDirection);
                joystickStrength = joyStickDirection.LengthSquared();
                const float joystickPathfindDistance = 1.0f;
                float moveDistance = joystickPathfindDistance;
                Vector3 rayStart = Entity.Transform.Position + new Vector3(0.0f, 2.0f, 0.0f) + moveDistance * normalizedStickDir;
                Vector3 rayEnd = rayStart + new Vector3(0.0f, -4.0f, 0.0f);
                HitResult result;
                if (this.GetSimulation().Raycast(
                    rayStart,
                    rayEnd,
                    out result,
                    CustomCollisionFilterGroups.ground.Value()))
                {
                    UpdateDestination(result.Point);
                    TestRayParticleSystem.Entity.Transform.Position = rayStart;
                    testRayEndTransform.Position = rayEnd;
                }
            }

            if (!isRunning)
            {
                RunSpeedEventKey.Broadcast(0);
                return;
            }

            UpdateMoveTowardsDestination(speed, joystickStrength);
        }
    }
}
