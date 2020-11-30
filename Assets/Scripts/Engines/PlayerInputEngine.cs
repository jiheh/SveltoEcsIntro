using System.Collections;
using Svelto.Tasks;
using UnityEngine;

namespace Svelto.ECS.Intro.Player {
    public class PlayerInputEngine : IQueryingEntitiesEngine {
        public EntitiesDB entitiesDB { private get; set; }

        public void Ready() {
            ReadInput().RunOnScheduler(StandardSchedulers.earlyScheduler);
        }

        IEnumerator ReadInput() {
            void IteratePlayersInput() {
                var h = Input.GetAxisRaw("Horizontal");
                var v = Input.GetAxisRaw("Vertical");

                var (inputComponents, count) = entitiesDB.QueryEntities<InputComponent>(ECSGroups.PlayersGroup);

                for (int i = 0; i < count; i++) {
                    inputComponents[i].value = new Vector3(h, 0f, v);
                    Debug.Log(inputComponents[i].value);
                }
            }

            while (true) {
                IteratePlayersInput();
                yield return null;
            }
        }
    }
}
