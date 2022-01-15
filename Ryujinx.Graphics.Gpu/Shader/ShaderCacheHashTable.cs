using Ryujinx.Common.Cache;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ryujinx.Graphics.Gpu.Shader
{
    class ShaderCacheHashTable
    {
        private struct IdCache
        {
            private PartitionedHashTable<int> _cache;
            private int _id;

            public void Initialize()
            {
                _cache = new PartitionedHashTable<int>();
                _id = 0;
            }

            public int Add(ReadOnlySpan<byte> code)
            {
                int id = ++_id;
                int cachedId = _cache.GetOrAdd(code, id);
                if (cachedId != id)
                {
                    --_id;
                }

                return cachedId;
            }

            public bool TryFind(IDataAccessor dataAccessor, out int id)
            {
                return _cache.TryFindItem(dataAccessor, out id);
            }
        }

        private struct IdTable : IEquatable<IdTable>
        {
            public int VertexAId;
            public int VertexBId;
            public int TessControlId;
            public int TessEvaluationId;
            public int GeometryId;
            public int FragmentId;

            public override bool Equals(object obj)
            {
                return obj is IdTable other && Equals(other);
            }

            public bool Equals(IdTable other)
            {
                return other.VertexAId == VertexAId &&
                       other.VertexBId == VertexBId &&
                       other.TessControlId == TessControlId &&
                       other.TessEvaluationId == TessEvaluationId &&
                       other.GeometryId == GeometryId &&
                       other.FragmentId == FragmentId;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(VertexAId, VertexBId, TessControlId, TessEvaluationId, GeometryId, FragmentId);
            }
        }

        private IdCache _vertexACache;
        private IdCache _vertexBCache;
        private IdCache _tessControlCache;
        private IdCache _tessEvaluationCache;
        private IdCache _geometryCache;
        private IdCache _fragmentCache;

        private readonly Dictionary<IdTable, ShaderBundle> _shaderPrograms;

        public ShaderCacheHashTable()
        {
            _vertexACache.Initialize();
            _vertexBCache.Initialize();
            _tessControlCache.Initialize();
            _tessEvaluationCache.Initialize();
            _geometryCache.Initialize();
            _fragmentCache.Initialize();

            _shaderPrograms = new Dictionary<IdTable, ShaderBundle>();
        }

        public void Add(ShaderBundle shaderBundle)
        {
            IdTable idTable = new IdTable();

            foreach (ShaderCodeHolder holder in shaderBundle.Shaders)
            {
                if (holder == null)
                {
                    continue;
                }

                switch (holder.Program.Stage)
                {
                    case ShaderStage.Vertex:
                        idTable.VertexBId = _vertexBCache.Add(holder.Code);
                        break;
                    case ShaderStage.TessellationControl:
                        idTable.TessControlId = _tessControlCache.Add(holder.Code);
                        break;
                    case ShaderStage.TessellationEvaluation:
                        idTable.TessEvaluationId = _tessEvaluationCache.Add(holder.Code);
                        break;
                    case ShaderStage.Geometry:
                        idTable.GeometryId = _geometryCache.Add(holder.Code);
                        break;
                    case ShaderStage.Fragment:
                        idTable.FragmentId = _fragmentCache.Add(holder.Code);
                        break;
                }

                if (holder.Code2 != null)
                {
                    idTable.VertexAId = _vertexACache.Add(holder.Code2);
                }
            }

            System.Console.WriteLine($"ids {idTable.VertexBId} {idTable.GeometryId} {idTable.FragmentId} total {_shaderPrograms.Count}");

            _shaderPrograms.Add(idTable, shaderBundle);
        }

        public bool TryFind(MemoryManager memoryManager, ShaderAddresses addresses, out ShaderBundle shaderBundle)
        {
            IdTable idTable = new IdTable();

            shaderBundle = null;

            if (!TryGetId(_vertexACache, memoryManager, addresses.VertexA, out idTable.VertexAId))
            {
                return false;
            }

            if (!TryGetId(_vertexBCache, memoryManager, addresses.Vertex, out idTable.VertexBId))
            {
                return false;
            }

            if (!TryGetId(_tessControlCache, memoryManager, addresses.TessControl, out idTable.TessControlId))
            {
                return false;
            }

            if (!TryGetId(_tessEvaluationCache, memoryManager, addresses.TessEvaluation, out idTable.TessEvaluationId))
            {
                return false;
            }

            if (!TryGetId(_geometryCache, memoryManager, addresses.Geometry, out idTable.GeometryId))
            {
                return false;
            }

            if (!TryGetId(_fragmentCache, memoryManager, addresses.Fragment, out idTable.FragmentId))
            {
                return false;
            }

            return _shaderPrograms.TryGetValue(idTable, out shaderBundle);
        }

        private static bool TryGetId(IdCache idCache, MemoryManager memoryMamanger, ulong baseAddress, out int id)
        {
            if (baseAddress == 0)
            {
                id = 0;
                return true;
            }

            ShaderCodeAccessor codeAccessor = new ShaderCodeAccessor(memoryMamanger, baseAddress);
            return idCache.TryFind(codeAccessor, out id);
        }
    }
}