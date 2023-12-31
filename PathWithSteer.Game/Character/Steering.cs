using Stride.Core;
using Stride.Engine;
using Stride.Physics;
using Stride.Core.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PathWithSteer.Core;
using Stride.Particles.Components;
using Stride.Core.Collections;
using SharpFont.PostScript;
using Stride.Core.Extensions;
using Silk.NET.SDL;
using ServiceWire;

namespace PathWithSteer.Character
{
    public class Steering
    {
        [DataMemberIgnore]
        public float CurrentMaxVelocity { get; set; }

        [DataMemberIgnore]
        public float CurrentMaxSteer { get; set; } = 0.175f;

        [DataMemberIgnore]
        public List<Vector3> CurrentPath 
        { 
            get => currentPath; 
            set 
            {
                CurrentWaypoint = 1;
                currentPath = value; 
            } 
        }

        [DataMemberIgnore]
        public bool ReachedDestination { get { return CurrentPath == null || CurrentWaypoint >= CurrentPath.Count; } }
        public ParticleSystemComponent TestRayParticleSystem { get; internal set; }
        public TransformComponent TestRayEndTransform { get; internal set; }

        public int CurrentWaypoint { get; private set; } = 0;
        private float wanderAngle = 0.0f;

        private Random random = new Random();
        private CharacterComponent character;
        private List<Vector3> currentPath = null;
        private PhysicsComponent currentObstacle;
        private Stride.Core.Diagnostics.Logger logger;

        public Steering(CharacterComponent character, Stride.Core.Diagnostics.Logger log)
        {
            this.character = character;
            logger = log;
            log.ActivateLog(Stride.Core.Diagnostics.LogMessageType.Debug);

        }

        public Vector3 Steer() 
        {
            
            Vector3 steering = Vector3.Zero;
            if(CurrentPath != null && CurrentPath.Count > 1)
            {
                steering += FollowPath();
            }
            steering += AvoidObstacles();
            steering.ClampLength(CurrentMaxSteer);
            return SteerVelocity(steering);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seekTarget">The target to seek in world space.</param>
        public Vector3 Seek(Vector3 seekTarget)
        {
            Vector3 desiredVelocity = seekTarget - character.Entity.Transform.WorldMatrix.TranslationVector;
            return desiredVelocity.ClampLength(CurrentMaxSteer);
        }

        public Vector3 SeekAndArrive(Vector3 seekTarget, float slowdownRadius, float arriveDistance = 0)
        {
            Vector3 desiredVelocity = seekTarget - character.Entity.Transform.WorldMatrix.TranslationVector;
            float length = desiredVelocity.Length();
            if (length < slowdownRadius)
            {
                //If we get close "enough" as specified by the arrive distance then we should stop. I.E., this will be 0
                float distanceToArrive = MathF.Max(length - arriveDistance, 0.0f);
                //Then linearly scale from the radius how fast we should steer.
                desiredVelocity = desiredVelocity.ClampLength(CurrentMaxVelocity) * (distanceToArrive / slowdownRadius);
            }
            Vector3 currentVelocity = character.VelocityInUnitsPerSecond();
            Vector3 steering = desiredVelocity - currentVelocity;
            Vector3 newVelocity = SteerVelocity(steering);
            if (newVelocity.LengthSquared() < CurrentMaxSteer * CurrentMaxSteer)
            {
                newVelocity = Vector3.Zero;
            }
            return newVelocity;
        }

        public Vector3 Flee(Vector3 fleeTarget, Vector3 worldPosition)
        {
            Vector3 desiredVelocity = worldPosition - fleeTarget;
            Vector3 steering = desiredVelocity.ClampLength(CurrentMaxSteer);
            return steering;
        }

        public Vector3 Wander(Vector3 currentVelocity)
        {
            const float WANDER_CIRCLE_DISTANCE = 2.0f;
            const float WANDER_CIRCLE_RADIUS = 1.0f;
            const float ANGLE_CHANGE = 0.07f; 
            Vector3 circleCenter = Vector3.Normalize(currentVelocity) * WANDER_CIRCLE_DISTANCE;
            Vector3 displacement = Vector3.UnitZ * WANDER_CIRCLE_RADIUS;
            var steerRotation = Quaternion.RotationYawPitchRoll(wanderAngle, 0, 0);
            displacement = steerRotation * displacement;
            wanderAngle += (random.NextSingle() * ANGLE_CHANGE) - (ANGLE_CHANGE * 0.5f);
            Vector3 wanderForce = circleCenter + displacement;
            return wanderForce;
        }

        public Vector3 Persue(CharacterController persuitTarget)
        {
            float persuerDistance = (persuitTarget.Entity.Transform.WorldMatrix.TranslationVector - character.Entity.Transform.WorldMatrix.TranslationVector).Length() / CurrentMaxVelocity;
            Vector3 futurePersuitPos = persuitTarget.Entity.Transform.WorldMatrix.TranslationVector + (persuitTarget.currentVelocity * persuerDistance);
            return Seek(futurePersuitPos);
        }

        public Vector3 Evade(CharacterController persuitTarget, Vector3 worldPosition)
        {
            float persuerDistance = (persuitTarget.Entity.Transform.WorldMatrix.TranslationVector - worldPosition).Length() / CurrentMaxVelocity;
            Vector3 futurePersuitPos = persuitTarget.Entity.Transform.WorldMatrix.TranslationVector + (persuitTarget.currentVelocity * persuerDistance);
            return Flee(futurePersuitPos, worldPosition);
        }

        //public Vector3 AvoidObstacles(Vector3 currentSteer)
        //{
        //    float lookDistance = CurrentMaxVelocity / 2.0f;
        //    float maxAvoidance = CurrentMaxSteer;
        //    Vector3 sweepDirection = Vector3.Normalize(character.LinearVelocity);
        //    Vector3 lookAhead = character.Entity.Transform.WorldMatrix.TranslationVector + (sweepDirection * lookDistance);
        //    Matrix toMatrix = character.Entity.Transform.WorldMatrix;
        //    toMatrix.TranslationVector = lookAhead;
        //    Vector3 avoidance = Vector3.Zero;
        //    PhysicsComponent collider = SweepForNearestCapsuleCollider(character, toMatrix, CollisionFilterGroupFlags.CharacterFilter);
        //    if (collider != null)
        //    {
        //        CapsuleColliderShape colliderAsCapsule =collider.ColliderShape as CapsuleColliderShape;
        //        if(colliderAsCapsule != null)
        //        {
        //            Vector3 colliderCenter = collider.Entity.Transform.WorldMatrix.TranslationVector + collider.ColliderShape.LocalOffset;
        //            avoidance = new Vector3(lookAhead.X - colliderCenter.X, 0.0f, lookAhead.Z - colliderCenter.Z);
        //            avoidance.ClampLength(maxAvoidance);
        //            TestRayParticleSystem.Entity.Transform.Position = character.Entity.Transform.WorldMatrix.TranslationVector + avoidance;
        //            TestRayEndTransform.Position = TestRayParticleSystem.Entity.Transform.Position + new Vector3(0.0f, 2.0f, 0.0f);

        //            TestRayParticleSystem.Entity.Transform.Position = character.Entity.Transform.WorldMatrix.TranslationVector + sweepDirection;
        //        }
        //    }
        //    return avoidance;
        //}

        public Vector3 AvoidObstacles()
        {
            float lookDistance = CurrentMaxVelocity / 2.0f;
            Vector3 sweepDirection = Vector3.Normalize(character.LinearVelocity);

            Vector3 lookAhead = character.Entity.Transform.WorldMatrix.TranslationVector + (sweepDirection * lookDistance);
            Matrix toMatrix = character.Entity.Transform.WorldMatrix;
            toMatrix.TranslationVector = lookAhead;
            Vector3 avoidance = Vector3.Zero;
            PhysicsComponent collider = SweepForNearestCapsuleCollider(character, toMatrix, CollisionFilterGroupFlags.CharacterFilter);

            if (currentObstacle != null)
            {
                Vector3 obstacleDistance = currentObstacle.Entity.GetWorldPosition() - character.Entity.GetWorldPosition();
                float dotToObstacle = Vector3.Dot(sweepDirection, obstacleDistance);
                if (dotToObstacle <= 0.0f)
                {
                    //We passed the obstacle. Replace it with the next one (may be null).
                    currentObstacle = collider;
                }
            }
            else
            {
                currentObstacle = collider;
            }
            
            if (currentObstacle != null)
            {
                CapsuleColliderShape colliderAsCapsule = currentObstacle.ColliderShape as CapsuleColliderShape;
                if (colliderAsCapsule != null)
                {
                    float maxAvoidance = (colliderAsCapsule.Radius + (character.ColliderShape as CapsuleColliderShape).Radius) / 2.0f;

                    Vector3 obstacleOffset = currentObstacle.Entity.GetWorldPosition() - character.Entity.GetWorldPosition();
                    //Get the vector 90deg to the right of the velocity by swizzling x and z
                    Vector3 rightSweepVector = Vector3.Cross(in sweepDirection, Vector3.UnitY);
                    float obstacleDot = Vector3.Dot(Vector3.Normalize(obstacleOffset), rightSweepVector);
                    //"invert" the dot product, so that lower absolute values are higher. Then multiply it by max avoidance
                    obstacleDot = (MathF.Sign(obstacleDot) - obstacleDot) * maxAvoidance * -1.0f;
                    avoidance = rightSweepVector * obstacleDot;
                    avoidance = avoidance * (lookDistance / obstacleOffset.Length());
                    //TestRayParticleSystem.Entity.Transform.Position = character.Entity.Transform.WorldMatrix.TranslationVector + avoidance;
                    //TestRayEndTransform.Position = TestRayParticleSystem.Entity.Transform.Position + new Vector3(0.0f, 2.0f, 0.0f);
                }
            }
            return avoidance;
        }

        public Vector3 FollowPath()
        {
            if (!ReachedDestination)
            {
                Vector3 previousWaypoint = CurrentPath[CurrentWaypoint - 1];
                Vector3 lineDirection = CurrentPath[CurrentWaypoint] - previousWaypoint;
                float lineLength = lineDirection.Length();
                lineDirection.Normalize();

                //The basis for movement is the current position + the max velocity in the direction of the current path segment.
                Vector3 movementGoal = character.Entity.Transform.WorldMatrix.TranslationVector + (lineDirection * CurrentMaxVelocity);

                //project that point onto the path segment
                Vector3 pointToLineStart = movementGoal - previousWaypoint;
                float dotProduct = Vector3.Dot(pointToLineStart, lineDirection);

                dotProduct = Math.Clamp(dotProduct, 0, lineLength);

                Vector3 currentGoal = previousWaypoint + lineDirection * dotProduct;

                Vector3 characterToLineStart = character.Entity.Transform.WorldMatrix.TranslationVector - previousWaypoint;
                float characterDot = Vector3.Dot(characterToLineStart, lineDirection);
                if(characterDot > lineLength)
                {
                    ++CurrentWaypoint;
                }

                if(CurrentWaypoint < CurrentPath.Count)
                {
                    return Seek(currentGoal);
                }
                else
                {
                    //This is the last waypoint. Arrive slowly.
                    return SeekAndArrive(currentGoal, 3.0f);
                }
            }
            else
            {
                return Vector3.Zero;
            }
        }

        private Vector3 SteerVelocity(Vector3 steering) 
        {
            return (character.VelocityInUnitsPerSecond() + steering).ClampLength(CurrentMaxVelocity);
        }

        private PhysicsComponent SweepForNearestCapsuleCollider(PhysicsComponent originPhysicsComponent, Matrix toMatrix, CollisionFilterGroupFlags filterGroupFlags) 
        {
            var result = new FastList<HitResult>();
            originPhysicsComponent.Simulation.ShapeSweepPenetrating(originPhysicsComponent.ColliderShape, originPhysicsComponent.PhysicsWorldTransform, toMatrix, result, (CollisionFilterGroups)(-1), CollisionFilterGroupFlags.CharacterFilter);
            float nearestDistanceSquared = -1.0f;
            PhysicsComponent nearestHit = null;
            for(int hitIndex = 0; hitIndex < result.Count; ++hitIndex)
            {
                CapsuleColliderShape colliderAsCapsule = result[hitIndex].Collider.ColliderShape as CapsuleColliderShape;
                if (colliderAsCapsule != null && result[hitIndex].Collider != originPhysicsComponent)
                {
                    float distanceSquared = Vector3.DistanceSquared(originPhysicsComponent.PhysicsWorldTransform.TranslationVector, result[hitIndex].Collider.PhysicsWorldTransform.TranslationVector);
                    if (nearestDistanceSquared == -1.0f || distanceSquared <= nearestDistanceSquared)
                    {
                        nearestHit = result[hitIndex].Collider;
                        nearestDistanceSquared = distanceSquared;
                    }
                }
            }
            return nearestHit;
        }
    }
}
