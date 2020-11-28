using System.Collections.Generic;
using UnityEngine;

namespace Svelto.ECS.Intro.ResourceManager {
    public class GameObjectFactory {

        readonly Dictionary<string, GameObject> _prefabs;

        public GameObjectFactory() {
            _prefabs = new Dictionary<string, GameObject>();
        }

        public IEnumerator<GameObject> Build(string prefabName) {
            if (_prefabs.TryGetValue(prefabName, out var go) == false) {
                var load = Resources.LoadAsync<GameObject>(prefabName);

                while (load.isDone == false) yield return null;

                go = (GameObject)load.asset;
                _prefabs.Add(prefabName, go);
            }

            yield return GameObject.Instantiate(go);
        }
    }
}
