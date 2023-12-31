// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using Stride.Animations;
using Stride.Engine;
using Stride.Engine.Events;
using PathWithSteer.Player;
using PathWithSteer.Gameplay;
using PathWithSteer.Character;

namespace PathWithSteer.Core.Animation
{
    public class AnimationController : SyncScript, IBlendTreeBuilder
    {
        [Display("Animation Component")]
        public AnimationComponent AnimationComponent { get; set; }

        [Display("Idle")]
        public AnimationClip AnimationIdle { get; set; }

        [Display("Walk")]
        public AnimationClip AnimationWalk { get; set; }

        [Display("Run")]
        public AnimationClip AnimationRun { get; set; }

        [DataMemberRange(0, 1, 0.01, 0.1, 3)]
        [Display("Walk Threshold")]
        public float WalkThreshold { get; set; } = 0.25f;

        [Display("Time Scale")]
        public double TimeFactor { get; set; } = 1;

        public CharacterController CharacterController { get; set; }

        [Stride.Core.DataMemberIgnore]
        public AbstractAnimationState CurrentState { get; set; }

        private WalkingAnimationState walkingState;
        private StartInteractionState interactingState;

        // Internal state
        private EventReceiver<float> runSpeedEvent;
        private EventReceiver<bool> attackEvent;
        private EventReceiver<Interactable> interactableEventKey;

        public override void Start()
        {
            base.Start();

            if (AnimationComponent == null)
                throw new InvalidOperationException("The animation component is not set");

            if (AnimationIdle == null)
                throw new InvalidOperationException("Idle animation is not set");

            if (AnimationWalk == null)
                throw new InvalidOperationException("Walking animation is not set");

            if (CharacterController == null)
                throw new InvalidOperationException("CharacterController is not set");

            runSpeedEvent = new EventReceiver<float>(CharacterController.RunSpeedEventKey);
            attackEvent = new EventReceiver<bool>(CharacterController.IsAttackingEventKey);
            interactableEventKey = new EventReceiver<Interactable>(CharacterController.InteractableEventKey);

            // By setting a custom blend tree builder we can override the default behavior of the animation system
            //  Instead, BuildBlendTree(FastList<AnimationOperation> blendStack) will be called each frame
            AnimationComponent.BlendTreeBuilder = this;

            walkingState = new WalkingAnimationState(this);
            interactingState = new StartInteractionState(this);

            walkingState.AddEvaluator(WalkingAnimationState.idleAnimName, AnimationIdle);
            walkingState.AddEvaluator(WalkingAnimationState.walkAnimName, AnimationWalk);
            walkingState.AddEvaluator(WalkingAnimationState.runAnimName, AnimationRun);

            StartAnimationState(walkingState);
        }

        public override void Cancel()
        {
            walkingState.RemoveAllEvaluators();
            interactingState.RemoveAllEvaluators();
        }

        public override void Update()
        {
            float runSpeed;
            runSpeedEvent.TryReceive(out runSpeed);
            walkingState.RunSpeed = runSpeed;

            bool isAttackingNewValue;
            if (attackEvent.TryReceive(out isAttackingNewValue) && isAttackingNewValue && interactableEventKey.TryReceive(out Interactable interactable) && CurrentState != interactingState)
            {
                interactingState.nextState = walkingState;
                interactingState.AddEvaluator(StartInteractionState.startInteractName, interactable.InteractionAnimation);
                StartAnimationState(interactingState);
            }

            CurrentState.UpdateAnimation();
        }

        private void StartAnimationState(AbstractAnimationState animationState)
        {
            CurrentState = animationState;
            CurrentState.StartAnimation();
        }

        /// <summary>
        /// BuildBlendTree is called every frame from the animation system when the <see cref="AnimationComponent"/> needs to be evaluated
        /// It overrides the default behavior of the <see cref="AnimationComponent"/> by setting a custom blend tree
        /// </summary>
        /// <param name="blendStack">The stack of animation operations to be blended</param>
        public void BuildBlendTree(FastList<AnimationOperation> blendStack)
        {
            CurrentState.BuildStateBlendTree(blendStack);
        }
    }
}
