using UnityEngine;
using Svelto.ECS.Hybrid;

namespace Svelto.ECS.Intro.Player {
    // public struct PositionComponent : IEntityComponent {
    //     public Vector3 value;
    // }

    public interface IPositionComponent {
        Vector3 position { get; set; }
    }

    public struct PositionViewComponent : IEntityViewComponent {
        public IPositionComponent positionComponent;
        public EGID ID { get; set; }
    }
}
