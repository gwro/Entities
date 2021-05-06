using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
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
            AtomicSafetyHandle.CheckReadAndThrow(chunkComponentType.m_Safety0);
#endif
            var chunk = archetypeChunk.m_Chunk;
            var archetype = chunk->Archetype;
            ChunkDataUtility.GetIndexInTypeArray(chunk->Archetype, chunkComponentType.m_TypeIndex, ref chunkComponentType.m_TypeLookupCache);
            var typeIndexInArchetype = chunkComponentType.m_TypeLookupCache;
            if (typeIndexInArchetype == -1)
            {
                var emptyResult = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(null, 0, 0);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref emptyResult, chunkComponentType.m_Safety0);
#endif
                return emptyResult;
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (archetype->Types[typeIndexInArchetype].IsBuffer)
                throw new ArgumentException($"ArchetypeChunk.GetComponentDataAsByteArray cannot be called for IBufferElementData {TypeManager.GetType(chunkComponentType.m_TypeIndex)}");
#endif
            
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
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, chunkComponentType.m_Safety0);
#endif
            return result;
        }
        
        public static int GetTypeIndex(this ref DynamicComponentTypeHandle componentTypeHandle)
        {
            return componentTypeHandle.m_TypeIndex;
        }
    }
}
