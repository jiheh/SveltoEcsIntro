using System.Collections;
using Svelto.Tasks;

namespace Svelto.ECS.Intro.Player {
    public class PlayerMovementEngine : IQueryingEntitiesEngine {
        public EntitiesDB entitiesDB { private get; set; }

        public void Ready() { MovePlayer().RunOnScheduler(StandardSchedulers.physicScheduler); }

        IEnumerator MovePlayer() {
            void PlayersMovement() {
                var (inputs, players, count) = entitiesDB.QueryEntities<InputComponent, PositionViewComponent>(ECSGroups.PlayersGroup);

                for (int i = 0; i < count; i++) {
                    players[i].positionComponent.position = players[i].positionComponent.position + inputs[i].value;
                }
            }

            while (true) {
                PlayersMovement();
                yield return null;
            }
        }
    }
}
