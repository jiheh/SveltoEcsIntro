using System;
using System.Runtime.CompilerServices;
using Svelto.Common;
using Unity.Jobs.LowLevel.Unsafe;
using Allocator = Svelto.Common.Allocator;

namespace Svelto.ECS.DataStructures {
    public unsafe struct AtomicNativeBags : IDisposable {
#if UNITY_NATIVE     
        [global::Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif        
        NativeBag* _data;
        readonly Allocator _allocator;
        readonly uint _threadsCount;

        public uint count => _threadsCount;

        public AtomicNativeBags(Allocator allocator) {
            _allocator = allocator;
            _threadsCount = JobsUtility.MaxJobThreadCount + 1;

            var bufferSize = MemoryUtilities.SizeOf<NativeBag>();
            var bufferCount = _threadsCount;
            var allocationSize = bufferSize * bufferCount;

            var ptr = (byte*)MemoryUtilities.Alloc((uint)allocationSize, allocator);
            // MemoryUtilities.MemClear((IntPtr) ptr, (uint) allocationSize);

            for (int i = 0; i < bufferCount; i++) {
                var bufferPtr = (NativeBag*)(ptr + bufferSize * i);
                var buffer = new NativeBag(allocator);
                MemoryUtilities.CopyStructureToPtr(ref buffer, (IntPtr)bufferPtr);
            }

            _data = (NativeBag*)ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NativeBag GetBuffer(int index) {
            if (_data == null)
                throw new Exception("using invalid AtomicNativeBags");

            return ref MemoryUtilities.ArrayElementAsRef<NativeBag>((IntPtr)_data, index);
        }

        public void Dispose() {
            if (_data == null)
                throw new Exception("using invalid AtomicNativeBags");

            for (int i = 0; i < _threadsCount; i++) {
                GetBuffer(i).Dispose();
            }
            MemoryUtilities.Free((IntPtr)_data, _allocator);
            _data = null;
        }

        public void Clear() {
            if (_data == null)
                throw new Exception("using invalid AtomicNativeBags");

            for (int i = 0; i < _threadsCount; i++) {
                GetBuffer(i).Clear();
            }
        }
    }
}
