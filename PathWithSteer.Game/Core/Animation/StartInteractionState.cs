using Stride.Animations;
using Stride.Core.Collections;
using Stride.Engine;
using System;


namespace PathWithSteer.Core.Animation
{
    public class StartInteractionState : AbstractAnimationState
    {
        public static readonly string startInteractName = "startInteract";
        public AbstractAnimationState nextState;
        public StartInteractionState(AnimationController animController) : base(animController)
        {
        }

        public override void BuildStateBlendTree(FastList<AnimationOperation> blendStack)
        {
            blendStack.Add(AnimationOperation.NewPush(EvaluatorsByName[startInteractName],
                            TimeSpan.FromTicks((long)(CurrentTime * EvaluatorsByName[startInteractName].Clip.Duration.Ticks))));
        }

        public override void UpdateAnimation()
        {
            var speedFactor = 1;
            var currentTicks = TimeSpan.FromTicks((long)(CurrentTime * EvaluatorsByName[startInteractName].Clip.Duration.Ticks));
            var updatedTicks = currentTicks.Ticks + (long)(animationController.Game.DrawTime.Elapsed.Ticks * animationController.TimeFactor * speedFactor);

            if (updatedTicks < EvaluatorsByName[startInteractName].Clip.Duration.Ticks)
            {
                currentTicks = TimeSpan.FromTicks(updatedTicks);
                CurrentTime = ((double)currentTicks.Ticks / (double)EvaluatorsByName[startInteractName].Clip.Duration.Ticks);
            }
            else
            {
                EndAnimation();
            }
        }

        public override void EndAnimation() 
        {
            RemoveEvaluator(startInteractName);
            nextState.StartAnimation();
        }
    }
}