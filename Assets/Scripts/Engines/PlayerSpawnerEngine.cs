using System.Collections;
using System.Collections.Generic;
using Svelto.ECS.Intro.ResourceManager;
using UnityEngine;
using Svelto.ECS.Hybrid;

namespace Svelto.ECS.Intro.Player {
    public class PlayerSpawnerEngine : IQueryingEntitiesEngine {
        public EntitiesDB entitiesDB { private get; set; }

        readonly IEntityFactory _entityFactory;
        readonly GameObjectFactory _gameObjectFactory;

        public PlayerSpawnerEngine(GameObjectFactory gameObjectFactory, IEntityFactory entityFactory) {
            _gameObjectFactory = gameObjectFactory;
            _entityFactory = entityFactory;
        }

        public void Ready() {
            SpawnPlayer().Run();
        }

        IEnumerator SpawnPlayer() {
            // Build game object in Unity
            IEnumerator<GameObject> loadingAsync = _gameObjectFactory.Build("Prefabs/Player");
            yield return loadingAsync; // wait until the asset is loaded and the gameobject built
            GameObject player = loadingAsync.Current;

            IImplementor playerPositionImplementor = player.AddComponent<PositionImplementor>();

            // Build entity for ECS
            List<IImplementor> implementors = new List<IImplementor>();
            implementors.Add(playerPositionImplementor);

            var x = _entityFactory.BuildEntity<PlayerEntityDescriptor>(0, ECSGroups.PlayersGroup, implementors);
        }
    }
}
