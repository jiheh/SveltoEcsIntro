
namespace Svelto.ECS.Intro.Player {
    public class PlayerEntityDescriptor : IEntityDescriptor {
        static readonly IComponentBuilder[] _componentsToBuild =
        {
            new ComponentBuilder<PositionViewComponent>(),
            new ComponentBuilder<InputComponent>(),
        };

        public IComponentBuilder[] componentsToBuild => _componentsToBuild;
    }
}
