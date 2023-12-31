using Stride.Animations;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathWithSteer.Core.Animation
{
    public class WalkingAnimationState : AbstractAnimationState
    {
        public float RunSpeed { get; set; }

        public static readonly string idleAnimName = "idle";
        public static readonly string walkAnimName = "walk";
        public static readonly string runAnimName = "run";

        //the two animations that we blend together to get the current walk cycle.
        //Goes from idle to walk, or from walk to run
        private AnimationClipEvaluator fromAnimEvaluatorWalkLerp;
        private AnimationClipEvaluator toAnimEvaluatorWalkLerp;
        private float walkLerpFactor = 0.5f;

        public WalkingAnimationState(AnimationController animationController) : base(animationController)
        {
        }

        public override void StartAnimation()
        {
            base.StartAnimation();
            RunSpeed = 0.0f;
        }

        public override void UpdateAnimation()
        {
            if (RunSpeed < animationController.WalkThreshold)
            {
                walkLerpFactor = RunSpeed / animationController.WalkThreshold;
                walkLerpFactor = (float)Math.Sqrt(walkLerpFactor);  // Idle-Walk blend looks really weird, so skew the factor towards walking
                fromAnimEvaluatorWalkLerp = EvaluatorsByName[idleAnimName];
                toAnimEvaluatorWalkLerp = EvaluatorsByName[walkAnimName];
            }
            else
            {
                walkLerpFactor = (RunSpeed - animationController.WalkThreshold) / (1.0f - animationController.WalkThreshold);
                fromAnimEvaluatorWalkLerp = EvaluatorsByName[walkAnimName];
                toAnimEvaluatorWalkLerp = EvaluatorsByName[runAnimName];
            }

            // Use DrawTime rather than UpdateTime
            var time = animationController.Game.DrawTime;
            // This update function will account for animation with different durations, keeping a current time relative to the blended maximum duration
            long blendedMaxDuration =
                (long)MathUtil.Lerp(fromAnimEvaluatorWalkLerp.Clip.Duration.Ticks, toAnimEvaluatorWalkLerp.Clip.Duration.Ticks, walkLerpFactor);

            var currentTicks = TimeSpan.FromTicks((long)(CurrentTime * blendedMaxDuration));

            currentTicks = blendedMaxDuration == 0
                ? TimeSpan.Zero
                : TimeSpan.FromTicks((currentTicks.Ticks + (long)(time.Elapsed.Ticks * animationController.TimeFactor)) %
                                     blendedMaxDuration);

            CurrentTime = currentTicks.Ticks / (double)blendedMaxDuration;
        }


        public override void BuildStateBlendTree(FastList<AnimationOperation> blendStack)
        {
            // Note! The tree is laid out as a stack and has to be flattened before returning it to the animation system!
            blendStack.Add(AnimationOperation.NewPush(fromAnimEvaluatorWalkLerp,
                TimeSpan.FromTicks((long)(CurrentTime * fromAnimEvaluatorWalkLerp.Clip.Duration.Ticks))));
            blendStack.Add(AnimationOperation.NewPush(toAnimEvaluatorWalkLerp,
                TimeSpan.FromTicks((long)(CurrentTime * toAnimEvaluatorWalkLerp.Clip.Duration.Ticks))));
            blendStack.Add(AnimationOperation.NewBlend(CoreAnimationOperation.Blend, walkLerpFactor));
        }
    }
}
