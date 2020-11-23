using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    [NativeContainer]
    public unsafe struct UntypedBufferAccessor
    {
        [NativeDisableUnsafePtrRestriction]
        private byte* m_BasePointer;
        private int m_Length;
        private int m_Stride;
        private int m_InternalCapacity;
        private int m_ElementSize;
        private int m_Alignment;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool m_IsReadOnly;
#endif

        public int Length => m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety0;
        private AtomicSafetyHandle m_ArrayInvalidationSafety;

#pragma warning disable 0414 // assigned but its value is never used
        private int m_SafetyReadOnlyCount;
        private int m_SafetyReadWriteCount;
#pragma warning restore 0414

#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public UntypedBufferAccessor(byte* basePointer, int length, int stride, bool readOnly, int elementSize, int alignment, AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety, int internalCapacity)
        {
            m_BasePointer = basePointer;
            m_Length = length * elementSize;
            m_Stride = stride;
            m_Safety0 = safety;
            m_ArrayInvalidationSafety = arrayInvalidationSafety;
            m_IsReadOnly = readOnly;
            m_ElementSize = elementSize;
            m_SafetyReadOnlyCount = m_IsReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = m_IsReadOnly ? 0 : 2;
            m_InternalCapacity = internalCapacity;
            m_Alignment = alignment;
        }
#else
        public UntypedBufferAccessor(byte* basePointer, int length, int stride, int elementSize, int alignment, int internalCapacity)
        {
            m_BasePointer = basePointer;
            m_Length = length * elementSize;
            m_Stride = stride;
            m_ElementSize = elementSize;
            m_InternalCapacity = internalCapacity;
            m_Alignment = alignment;
        }
#endif

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void AssertIndexInRange(int index)
        {
            if (index < 0 || index >= Length)
                throw new InvalidOperationException($"index {index} out of range in LowLevelBufferAccessor of length {Length}");
        }

        public NativeArray<byte> this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
                AssertIndexInRange(index);
                BufferHeader* hdr = (BufferHeader*) (m_BasePointer + index * m_Stride);

                var shadow = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(BufferHeader.GetElementPointer(hdr), hdr->Length * m_ElementSize, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var handle = m_ArrayInvalidationSafety;
                AtomicSafetyHandle.UseSecondaryVersion(ref handle);
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref shadow, handle);
#endif
                return shadow;
            }
        }

        // length should be the real length, not the byte length
        public void ResizeBufferUninitialized(int index, int length)
        {
            BufferHeader* hdr = (BufferHeader*) (m_BasePointer + index * m_Stride);

            CheckWriteAccessAndInvalidateArrayAliases();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            BufferHeader.EnsureCapacity(hdr, length, m_ElementSize, m_Alignment, BufferHeader.TrashMode.RetainOldData, false, 0);
#else
            BufferHeader.EnsureCapacity(hdr, length, m_ElementSize, m_Alignment, BufferHeader.TrashMode.RetainOldData, false, 0);
#endif
            hdr->Length = length;
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWriteAccessAndInvalidateArrayAliases()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_ArrayInvalidationSafety);
#endif
        }
    }
    
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct DynamicBufferTypeHandle
    {
        internal readonly int m_TypeIndex;
        internal readonly uint m_GlobalSystemVersion;
        internal readonly bool m_IsReadOnly;
        
        public uint GlobalSystemVersion => m_GlobalSystemVersion;
        public bool IsReadOnly => m_IsReadOnly;
        
#pragma warning disable 0414
        private readonly int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly int m_MinIndex;
        private readonly int m_MaxIndex;
        
        internal readonly AtomicSafetyHandle m_Safety0;
        internal readonly AtomicSafetyHandle m_Safety1;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;
#endif
#pragma warning restore 0414
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal DynamicBufferTypeHandle(AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety, ComponentType componentType, uint globalSystemVersion)
#else
        internal DynamicBufferTypeHandle(ComponentType componentType, uint globalSystemVersion)
#endif
        {
            m_Length = 1;
            m_TypeIndex = componentType.TypeIndex;
            m_GlobalSystemVersion = globalSystemVersion;
            bool isReadOnly = componentType.AccessModeType == ComponentType.AccessMode.ReadOnly;
            m_IsReadOnly = isReadOnly;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = 0;
            m_Safety0 = safety;
            m_Safety1 = arrayInvalidationSafety;
            m_SafetyReadOnlyCount = isReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = isReadOnly ? 0 : 2;
#endif
        }
        
    }
    
    public static class UntypedAccessExtensionMethods
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckZeroSizedComponentData(DynamicComponentTypeHandle chunkComponentTypeHandle)
        {
            if (chunkComponentTypeHandle.m_IsZeroSized)
                throw new ArgumentException($"ArchetypeChunk.GetComponentDataAsByteArray cannot be called on zero-sized IComponentData");
        }
        
        // based on ArchetypeChunk::GetDynamicComponentDataArrayReinterpret
        public static unsafe NativeArray<byte> GetComponentDataAsByteArray(this ref ArchetypeChunk archetypeChunk, DynamicComponentTypeHandle chunkComponentType)
        {
            CheckZeroSizedComponentData(chunkComponentType);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(chunkComponentType.m_Safety);
#endif
            var chunk = archetypeChunk.m_Chunk;
            var archetype = chunk->Archetype;
            ChunkDataUtility.GetIndexInTypeArray(chunk->Archetype, chunkComponentType.m_TypeIndex, ref chunkComponentType.m_TypeLookupCache);
            var typeIndexInArchetype = chunkComponentType.m_TypeLookupCache;
            if (typeIndexInArchetype == -1)
            {
                var emptyResult = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(null, 0, 0);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref emptyResult, chunkComponentType.m_Safety);
#endif
                return emptyResult;
            }
            
            var typeSize = archetype->SizeOfs[typeIndexInArchetype];
            var length = archetypeChunk.Count;
            var byteLen = length * typeSize;
            // var outTypeSize = 1;
            // var outLength = byteLen / outTypeSize;
            
            byte* ptr = (chunkComponentType.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(chunk, 0, typeIndexInArchetype)
                : ChunkDataUtility.GetComponentDataRW(chunk, 0, typeIndexInArchetype, chunkComponentType.GlobalSystemVersion);
            
            var batchStartOffset = archetypeChunk.m_BatchStartEntityIndex * archetype->SizeOfs[typeIndexInArchetype];
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptr + batchStartOffset, byteLen, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, chunkComponentType.m_Safety);
#endif
            return result;
        }
        
        public static unsafe DynamicBufferTypeHandle GetDynamicBufferTypeHandle(this EntityManager entityManager, ComponentType componentType)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var access = entityManager.GetCheckedEntityDataAccess();
            var typeIndex = componentType.TypeIndex;
            return new DynamicBufferTypeHandle(
                access->DependencyManager->Safety.GetSafetyHandleForBufferTypeHandle(componentType.TypeIndex, componentType.AccessModeType == ComponentType.AccessMode.ReadOnly),
                access->DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(typeIndex),
                componentType, entityManager.GlobalSystemVersion);
#else
            return new DynamicBufferTypeHandle(componentType, entityManager.GlobalSystemVersion);
#endif
        }

        public static DynamicBufferTypeHandle GetDynamicBufferTypeHandle(this ComponentSystemBase system, ComponentType componentType)
        {
            system.AddReaderWriter(componentType);
            return system.EntityManager.GetDynamicBufferTypeHandle(componentType);
        }
        
        // based on ArchetypeChunk::GetBufferAccessor
        public static unsafe UntypedBufferAccessor GetUntypedBufferAccessor(this ref ArchetypeChunk chunk, DynamicBufferTypeHandle bufferComponentType)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferComponentType.m_Safety0);
#endif
            var archetype = chunk.m_Chunk->Archetype;
            var typeIndex = bufferComponentType.m_TypeIndex;
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
            if (typeIndexInArchetype == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new UntypedBufferAccessor(null, 0, 0, true, 0, 0, bufferComponentType.m_Safety0, bufferComponentType.m_Safety1, 0);
#else
                return new UntypedBufferAccessor(null, 0, 0, 0, 0, 0);
#endif
            }

            int internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];
            
            byte* ptr = (bufferComponentType.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(chunk.m_Chunk, 0, typeIndexInArchetype)
                : ChunkDataUtility.GetComponentDataRW(chunk.m_Chunk, 0, typeIndexInArchetype, bufferComponentType.GlobalSystemVersion);

            var length = chunk.m_Chunk->Count;
            int stride = archetype->SizeOfs[typeIndexInArchetype];
            var batchStartOffset = chunk.m_BatchStartEntityIndex * stride;
            var typeInfo = TypeManager.GetTypeInfo(bufferComponentType.m_TypeIndex);
            int elementSize = typeInfo.ElementSize;
            int alignment = typeInfo.AlignmentInChunkInBytes;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new UntypedBufferAccessor(ptr + batchStartOffset, length, stride, bufferComponentType.IsReadOnly, elementSize, alignment, bufferComponentType.m_Safety0, bufferComponentType.m_Safety1, internalCapacity);
#else
            return new UntypedBufferAccessor(ptr + batchStartOffset, length, stride, elementSize, alignment, internalCapacity);
#endif
        }
        
        // based on ArchetypeChunk::Has<BufferTypeHandle>()
        public static unsafe bool Has(this ref ArchetypeChunk chunk, DynamicBufferTypeHandle bufferTypeHandle)
        {
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(chunk.m_Chunk->Archetype, bufferTypeHandle.m_TypeIndex);
            return (typeIndexInArchetype != -1);
        }
        
        public static int GetTypeIndex(this ref DynamicComponentTypeHandle componentTypeHandle)
        {
            return componentTypeHandle.m_TypeIndex;
        }
        
        public static int GetTypeIndex(this ref DynamicBufferTypeHandle bufferComponentType)
        {
            return bufferComponentType.m_TypeIndex;
        }
    }
}
