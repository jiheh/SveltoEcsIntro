using Svelto.ECS.Hybrid;
using UnityEngine;

namespace Svelto.ECS.Intro.Player {
    public class PositionImplementor : MonoBehaviour, IPositionComponent, IImplementor {
        Transform _transform;

        public Vector3 position {
            get { return _transform.position; }
            set { _transform.position = value; }
        }

        void Awake() {
            _transform = transform;
        }
    }
}
