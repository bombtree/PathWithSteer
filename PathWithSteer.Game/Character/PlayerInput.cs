 // Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System.Linq;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Input;
using Stride.Physics;
using Stride.Rendering;
using PathWithSteer.Core;
using Stride.Core.Extensions;
using Stride.Graphics;
using System.Drawing;
using System.Windows;
using Stride.Core.Diagnostics;
using System.Runtime.Serialization;
using Stride.Core;

namespace PathWithSteer.Player
{
    public class PlayerInput : SyncScript
    {
        /// <summary>
        /// Raised every frame with the intended direction of movement from the player.
        /// </summary>
        [DataMemberIgnore]
        public readonly EventKey<ClickResult> MoveDestinationEventKey = new EventKey<ClickResult>();

        /// <summary>
        /// Direction of the joystick translated to a local vector relative to the camera direction.
        /// </summary>
        [DataMemberIgnore]
        public readonly EventKey<Vector3> JoystickLocalVectorEventKey = new EventKey<Vector3>();

        public int ControllerIndex { get; set; }

        public float DeadZone { get; set; } = 0.25f;

        public Entity Highlight { get; set; }

        public Material HighlightMaterial { get; set; }

        public CameraComponent Camera { get; set; }

        public Prefab ClickEffect { get; set; }

        /// <summary>
        /// Amount of time (in seconds) the player must hold down before we switch to directional moving.
        /// </summary>
        public float HoldToMoveCooldownTime { get; set; } = 0.1f;

        private float currentHoldTime = 0.0f;

        private ClickResult lastClickResult;

        private RenderContext renderContext;

        public override void Start()
        {
            base.Start();
            Log.ActivateLog(Stride.Core.Diagnostics.LogMessageType.Debug);
            renderContext = RenderContext.GetShared(Services);
        }

        public override void Update()
        {
            var screenPresses = Input.PointerEvents.Where(x => x.EventType == PointerEventType.Pressed);

            if (Input.HasMouse)
            {
                ClickResult clickResult;

                Utils.ScreenPositionToWorldPositionRaycast(Input.MousePosition, Camera, this.GetSimulation(), out clickResult);

                var isMoving = (Input.IsMouseButtonDown(MouseButton.Left));

                var isHighlit = (!isMoving && clickResult.Type == ClickType.LootCrate);

                // Character continuous moving
                if (isMoving)
                {
                    //Add the time since last frame.
                    currentHoldTime += (float)Game.UpdateTime.Elapsed.TotalSeconds;

                    if(currentHoldTime > HoldToMoveCooldownTime)
                    {

                        Viewport viewport = renderContext.ViewportState.Viewport0;
                        //Get the player screen position
                        var backBuffer = GraphicsDevice.Presenter.BackBuffer;
                        Vector3 playerScreenPos = Vector3.Project(Entity.Transform.Position, 0, 0, 1, 1, viewport.MinDepth, viewport.MaxDepth, Camera.ViewProjectionMatrix);

                        Vector2 relativeMousePosition = Input.MousePosition - playerScreenPos.XY();
                        const float maxScreenDistanceForTouch = 0.15f;
                        //Vector2 adjustedMousePosition = relativeMousePosition / maxScreenDistanceForTouch;
                        Vector2 virtualJoystickPos = Vector2.Normalize(relativeMousePosition) * System.Math.Min(relativeMousePosition.Length() / maxScreenDistanceForTouch, 1.0f);

                        JoystickLocalVectorEventKey.Broadcast(JoystickDirectionToLocalSpace(virtualJoystickPos));
                    }
                }
                else
                {
                    //The player is not holding down on the mouse. Reset the hold time
                    currentHoldTime = 0.0f;
                }

                // Object highlighting
                if (isHighlit)
                {
                    var modelComponentA = Highlight?.Get<ModelComponent>();
                    var modelComponentB = clickResult.ClickedEntity.Get<ModelComponent>();

                    if (modelComponentA != null && modelComponentB != null)
                    {
                        var materialCount = modelComponentB.Model.Materials.Count;
                        modelComponentA.Model = modelComponentB.Model;
                        modelComponentA.Materials.Clear();
                        for (int i = 0; i < materialCount; i++)
                            modelComponentA.Materials.Add(i, HighlightMaterial);

                        modelComponentA.Entity.Transform.UseTRS = false;
                        modelComponentA.Entity.Transform.LocalMatrix = modelComponentB.Entity.Transform.WorldMatrix;
                    }
                }
                else
                {
                    var modelComponentA = Highlight?.Get<ModelComponent>();
                    if (modelComponentA != null)
                        modelComponentA.Entity.Transform.LocalMatrix = Matrix.Scaling(0);
                }
            }

            foreach (var pointerEvent in screenPresses)
            {
                ClickResult clickResult;
                if (Utils.ScreenPositionToWorldPositionRaycast(pointerEvent.Position, Camera, this.GetSimulation(),
                    out clickResult))
                {
                    lastClickResult = clickResult;
                    MoveDestinationEventKey.Broadcast(clickResult);

                    if (ClickEffect != null && clickResult.Type == ClickType.Ground)
                    {
                        this.SpawnPrefabInstance(ClickEffect, null, 1.2f, Matrix.RotationQuaternion(Quaternion.BetweenDirections(Vector3.UnitY, clickResult.HitResult.Normal)) * Matrix.Translation(clickResult.WorldPosition));
                    }
                }
            }
        }

        /// <summary>
        /// Translates the joystick direction to local space with respect to the camera member variable. 
        /// E.g., if the camera is facing toward X+ then pressing forward will return a vector (1,0,0)
        /// </summary>
        /// <param name="joystickPosition">Current position of the joystick</param>
        /// <returns>A local space vector relative to this entity</returns>
        private Vector3 JoystickDirectionToLocalSpace(Vector2 joystickPosition)
        {
            Vector3 cameraPosition; //Just needed for the GetWorldTransformation() argument
            Quaternion cameraRotation;
            Vector3 cameraScale; //Just needed for the GetWorldTransformation() argument
            Camera.Entity.Transform.GetWorldTransformation(out cameraPosition, out cameraRotation, out cameraScale);

            Vector3 cameraForward = Vector3.UnitZ;
            cameraRotation.Rotate(ref cameraForward);
            cameraForward.Y = 0;
            cameraForward.Normalize();

            Vector3 cameraRight = Vector3.UnitX;
            cameraRotation.Rotate(ref cameraRight);
            cameraRight.Y = 0;
            cameraRight.Normalize();

            return (joystickPosition.X * cameraRight + joystickPosition.Y * cameraForward);
        }
    }
}
