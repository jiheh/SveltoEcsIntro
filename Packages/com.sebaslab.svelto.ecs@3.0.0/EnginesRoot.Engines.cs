using System;
using System.Collections.Generic;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.ECS.Experimental;
using Svelto.ECS.Internal;
using Svelto.ECS.Schedulers;

namespace Svelto.ECS {
    public sealed partial class EnginesRoot {
        public struct EntitiesSubmitter {
            public EntitiesSubmitter(EnginesRoot enginesRoot) {
                _weakReference = new Svelto.DataStructures.WeakReference<EnginesRoot>(enginesRoot);
            }

            public void Invoke() {
                if (_weakReference.IsValid)
                    _weakReference.Target.SubmitEntityComponents();
            }

            readonly Svelto.DataStructures.WeakReference<EnginesRoot> _weakReference;
        }

        public IEntitiesSubmissionScheduler scheduler { get; }

        /// <summary>
        /// Engines root contextualize your engines and entities. You don't need to limit yourself to one EngineRoot
        /// as multiple engines root could promote separation of scopes. The EntitySubmissionScheduler checks
        /// periodically if new entity must be submitted to the database and the engines. It's an external
        /// dependencies to be independent by the running platform as the user can define it.
        /// The EntitySubmissionScheduler cannot hold an EnginesRoot reference, that's why
        /// it must receive a weak reference of the EnginesRoot callback.
        /// </summary>
        public EnginesRoot(IEntitiesSubmissionScheduler entitiesComponentScheduler) {
            _entitiesOperations = new ThreadSafeDictionary<ulong, EntitySubmitOperation>();
            serializationDescriptorMap = new SerializationDescriptorMap();
            _reactiveEnginesAddRemove = new FasterDictionary<RefWrapperType, FasterList<IEngine>>();
            _reactiveEnginesSwap = new FasterDictionary<RefWrapperType, FasterList<IEngine>>();
            _reactiveEnginesSubmission = new FasterList<IReactOnSubmission>();
            _enginesSet = new FasterList<IEngine>();
            _enginesTypeSet = new HashSet<Type>();
            _disposableEngines = new FasterList<IDisposable>();
            _transientEntitiesOperations = new FasterList<EntitySubmitOperation>();

            _groupEntityComponentsDB =
                new FasterDictionary<ExclusiveGroupStruct, FasterDictionary<RefWrapperType, ITypeSafeDictionary>>();
            _groupsPerEntity = new FasterDictionary<RefWrapperType, FasterDictionary<ExclusiveGroupStruct, ITypeSafeDictionary>>();
            _groupedEntityToAdd = new DoubleBufferedEntitiesToAdd();

            _entityStreams = EntitiesStreams.Create();
            _groupFilters = new FasterDictionary<RefWrapperType, FasterDictionary<ExclusiveGroupStruct, GroupFilters>>();
            _entitiesDB = new EntitiesDB(this);

            scheduler = entitiesComponentScheduler;
            scheduler.onTick = new EntitiesSubmitter(this);
#if UNITY_NATIVE
            AllocateNativeOperations();
#endif
        }

        public EnginesRoot
            (IEntitiesSubmissionScheduler entitiesComponentScheduler, bool isDeserializationOnly) :
            this(entitiesComponentScheduler) {
            _isDeserializationOnly = isDeserializationOnly;
        }

        /// <summary>
        /// Dispose an EngineRoot once not used anymore, so that all the
        /// engines are notified with the entities removed.
        /// It's a clean up process.
        /// </summary>
        public void Dispose() {
            _isDisposing = true;

            using (var profiler = new PlatformProfiler("Final Dispose")) {
                //Note: The engines are disposed before the the remove callback to give the chance to behave
                //differently if a remove happens as a consequence of a dispose
                //The pattern is to implement the IDisposable interface and set a flag in the engine. The 
                //remove callback will then behave differently according the flag.
                foreach (var engine in _disposableEngines) {
                    try {
                        engine.Dispose();
                    } catch (Exception e) {
                        Svelto.Console.LogException(e);
                    }
                }

                foreach (FasterDictionary<ExclusiveGroupStruct, FasterDictionary<RefWrapperType, ITypeSafeDictionary>>.
                    KeyValuePairFast groups in _groupEntityComponentsDB) {
                    foreach (FasterDictionary<RefWrapperType, ITypeSafeDictionary>.KeyValuePairFast entityList in
                        groups.Value)
                        try {
                            entityList.Value.RemoveEntitiesFromEngines(_reactiveEnginesAddRemove, profiler
                                                                     , new ExclusiveGroupStruct(groups.Key));
                        } catch (Exception e) {
                            Svelto.Console.LogException(e);
                        }
                }

                foreach (FasterDictionary<ExclusiveGroupStruct, FasterDictionary<RefWrapperType, ITypeSafeDictionary>>.
                    KeyValuePairFast groups in _groupEntityComponentsDB) {
                    foreach (FasterDictionary<RefWrapperType, ITypeSafeDictionary>.KeyValuePairFast entityList in
                        groups.Value)
                        entityList.Value.Dispose();
                }

                foreach (FasterDictionary<RefWrapperType, FasterDictionary<ExclusiveGroupStruct, GroupFilters>>.KeyValuePairFast type in _groupFilters)
                    foreach (FasterDictionary<ExclusiveGroupStruct, GroupFilters>.KeyValuePairFast group in type.Value)
                        group.Value.Dispose();

                _groupFilters.Clear();

#if UNITY_NATIVE                
                _addOperationQueue.Dispose();
                _removeOperationQueue.Dispose();
                _swapOperationQueue.Dispose();
#endif
                _groupEntityComponentsDB.Clear();
                _groupsPerEntity.Clear();

                _disposableEngines.Clear();
                _enginesSet.Clear();
                _enginesTypeSet.Clear();
                _reactiveEnginesSwap.Clear();
                _reactiveEnginesAddRemove.Clear();
                _reactiveEnginesSubmission.Clear();

                _entitiesOperations.Clear();
                _transientEntitiesOperations.Clear();

                _groupedEntityToAdd.Dispose();
                _entityStreams.Dispose();

                scheduler.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        ~EnginesRoot() {
            Console.LogWarning("Engines Root has been garbage collected, don't forget to call Dispose()!");

            Dispose();
        }

        public void AddEngine(IEngine engine) {
            var type = engine.GetType();
            var refWrapper = new RefWrapperType(type);
            DBC.ECS.Check.Require(engine != null, "Engine to add is invalid or null");
            DBC.ECS.Check.Require(
                _enginesTypeSet.Contains(refWrapper) == false
             || type.ContainsCustomAttribute(typeof(AllowMultipleAttribute)) == true
              , "The same engine has been added more than once, if intentional, use [AllowMultiple] class attribute "
                   .FastConcat(engine.ToString()));
            try {
                if (engine is IReactOnAddAndRemove viewEngine)
                    CheckEntityComponentsEngine(viewEngine, _reactiveEnginesAddRemove);

                if (engine is IReactOnSwap viewEngineSwap)
                    CheckEntityComponentsEngine(viewEngineSwap, _reactiveEnginesSwap);

                if (engine is IReactOnSubmission submissionEngine)
                    _reactiveEnginesSubmission.Add(submissionEngine);

                _enginesTypeSet.Add(refWrapper);
                _enginesSet.Add(engine);

                if (engine is IDisposable)
                    _disposableEngines.Add(engine as IDisposable);

                if (engine is IQueryingEntitiesEngine queryableEntityComponentEngine) {
                    queryableEntityComponentEngine.entitiesDB = _entitiesDB;
                    queryableEntityComponentEngine.Ready();
                }
            } catch (Exception e) {
                throw new ECSException("Code crashed while adding engine ".FastConcat(engine.GetType().ToString(), " ")
                                     , e);
            }
        }

        void CheckEntityComponentsEngine<T>(T engine, FasterDictionary<RefWrapperType, FasterList<IEngine>> engines)
            where T : class, IEngine {
            var interfaces = engine.GetType().GetInterfaces();

            foreach (var interf in interfaces) {
                if (interf.IsGenericTypeEx() && typeof(T).IsAssignableFrom(interf)) {
                    var genericArguments = interf.GetGenericArgumentsEx();

                    AddEngine(engine, genericArguments, engines);
                }
            }
        }

        static void AddEngine<T>
            (T engine, Type[] entityComponentTypes, FasterDictionary<RefWrapperType, FasterList<IEngine>> engines)
            where T : class, IEngine {
            for (var i = 0; i < entityComponentTypes.Length; i++) {
                var type = entityComponentTypes[i];

                AddEngine(engine, engines, type);
            }
        }

        static void AddEngine<T>(T engine, FasterDictionary<RefWrapperType, FasterList<IEngine>> engines, Type type)
            where T : class, IEngine {
            if (engines.TryGetValue(new RefWrapperType(type), out var list) == false) {
                list = new FasterList<IEngine>();

                engines.Add(new RefWrapperType(type), list);
            }

            list.Add(engine);
        }

        readonly FasterDictionary<RefWrapperType, FasterList<IEngine>> _reactiveEnginesAddRemove;
        readonly FasterDictionary<RefWrapperType, FasterList<IEngine>> _reactiveEnginesSwap;
        readonly FasterList<IReactOnSubmission> _reactiveEnginesSubmission;
        readonly FasterList<IDisposable> _disposableEngines;
        readonly FasterList<IEngine> _enginesSet;
        readonly HashSet<Type> _enginesTypeSet;
        internal bool _isDisposing;
    }
}