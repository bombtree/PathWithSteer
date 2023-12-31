using Stride.Animations;
using Stride.Core.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathWithSteer.Core.Animation
{
    public abstract class AbstractAnimationState
    {
        public double CurrentTime { get; protected set; }
        protected Dictionary<string, AnimationClipEvaluator> EvaluatorsByName { get; set; }
        protected AnimationController animationController;
        
        public abstract void BuildStateBlendTree(FastList<AnimationOperation> blendStack);

        public virtual void StartAnimation()
        {
            animationController.CurrentState = this;
            CurrentTime = 0.0f;
            UpdateAnimation();
        }

        public abstract void UpdateAnimation();

        public virtual void EndAnimation() { }

        public AbstractAnimationState(AnimationController animController) 
        {
            animationController = animController;
            EvaluatorsByName = new Dictionary<string, AnimationClipEvaluator>();
        }

        public void AddEvaluator(string name, AnimationClip animationClip)
        {
            if (EvaluatorsByName.ContainsKey(name))
            {
                RemoveEvaluator(name);
            }
            EvaluatorsByName.Add(name, animationController.AnimationComponent.Blender.CreateEvaluator(animationClip));
        }

        public void RemoveEvaluator(string name)
        {
            animationController.AnimationComponent.Blender.ReleaseEvaluator(EvaluatorsByName[name]);
            EvaluatorsByName.Remove(name);
        }

        public void RemoveAllEvaluators()
        {
            foreach(var kvp in EvaluatorsByName)
            {
                animationController.AnimationComponent.Blender.ReleaseEvaluator(kvp.Value);
            }
            EvaluatorsByName.Clear();
        }
    }
}
