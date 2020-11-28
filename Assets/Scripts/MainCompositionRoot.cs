using Svelto.Context;
using Svelto.ECS.Schedulers.Unity;
using Svelto.ECS.Intro.Player;
using Svelto.Tasks;
using Svelto.ECS.Intro.ResourceManager;

namespace Svelto.ECS.Intro {
    public class MainCompositionRoot : ICompositionRoot {
        EnginesRoot _enginesRoot;

        public void OnContextCreated<T>(T contextHolder) { }

        public void OnContextInitialized<T>(T contextHolder) {
            CompositionRoot();
        }

        public void OnContextDestroyed() {
            _enginesRoot.Dispose();

            TaskRunner.StopAndCleanupAllDefaultSchedulers();
        }

        void CompositionRoot() {
            _enginesRoot = new EnginesRoot(new UnityEntitiesSubmissionScheduler());

            var entityFactory = _enginesRoot.GenerateEntityFactory();
            var gameObjectFactory = new GameObjectFactory();

            var playerSpawnerEngine = new PlayerSpawnerEngine(gameObjectFactory, entityFactory);
            _enginesRoot.AddEngine(playerSpawnerEngine);

            var playerInputEngine = new PlayerInputEngine();
            _enginesRoot.AddEngine(playerInputEngine);

            var playerMovementEngine = new PlayerMovementEngine();
            _enginesRoot.AddEngine(playerMovementEngine);
        }
    }
}
