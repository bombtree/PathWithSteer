// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Stride.Core.Collections;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Navigation;

namespace Gameplay
{
    public class EnableDynamicNavigation : SyncScript
    {
        public DynamicNavigationMeshSystem dynamicNavigationMeshSystem { get; set; }

        public override void Start()
        {
            var dynamicNavSystem = Game.GameSystems.OfType<DynamicNavigationMeshSystem>().FirstOrDefault();
            // Wait for the dynamic navigation to be registered
            if (dynamicNavSystem == null)
            {
                Game.GameSystems.CollectionChanged += GameSystemsOnCollectionChanged;
            }
            else
            {
                SetupDynamicNavigationMeshSystem(dynamicNavSystem);
            }
        }

        private void UpdateDynamicNavigation(object sender, NavigationMeshUpdatedEventArgs e)
        {
            //foreach(var component in DynamicNavUpdaterComponents)
            //{
            //    component?.OnNavMeshChanged();
            //}
        }

        public override void Update()
        {
        }

        public override void Cancel()
        {
            Game.GameSystems.CollectionChanged -= GameSystemsOnCollectionChanged;
        }

        private void GameSystemsOnCollectionChanged(object sender, TrackingCollectionChangedEventArgs trackingCollectionChangedEventArgs)
        {
            if (trackingCollectionChangedEventArgs.Action == NotifyCollectionChangedAction.Add)
            {
                var dynamicNavSystem = trackingCollectionChangedEventArgs.Item as DynamicNavigationMeshSystem;
                if (dynamicNavSystem != null)
                {
                    SetupDynamicNavigationMeshSystem(dynamicNavSystem);

                    // No longer need to listen to changes
                    Game.GameSystems.CollectionChanged -= GameSystemsOnCollectionChanged;
                }
            }
        }

        private void SetupDynamicNavigationMeshSystem(DynamicNavigationMeshSystem dynamicNavSystem)
        {
            dynamicNavigationMeshSystem = dynamicNavSystem;
            dynamicNavigationMeshSystem.Enabled = true;
            dynamicNavigationMeshSystem.NavigationMeshUpdated += UpdateDynamicNavigation;
            dynamicNavigationMeshSystem.AutomaticRebuild = true;
        }
    }
}
