﻿using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// Represents a GPU state and memory accessor.
    /// </summary>
    class GpuAccessor : TextureDescriptorCapableGpuAccessor, IGpuAccessor
    {
        private readonly GpuChannel _channel;
        private readonly GpuAccessorState _state;
        private readonly int _stageIndex;
        private readonly bool _compute;
        private readonly int _localSizeX;
        private readonly int _localSizeY;
        private readonly int _localSizeZ;
        private readonly int _localMemorySize;
        private readonly int _sharedMemorySize;

        public int Cb1DataSize { get; private set; }

        /// <summary>
        /// Creates a new instance of the GPU state accessor for graphics shader translation.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="state">Current GPU state</param>
        /// <param name="stageIndex">Graphics shader stage index (0 = Vertex, 4 = Fragment)</param>
        public GpuAccessor(GpuContext context, GpuChannel channel, GpuAccessorState state, int stageIndex) : base(context)
        {
            _channel = channel;
            _state = state;
            _stageIndex = stageIndex;
        }

        /// <summary>
        /// Creates a new instance of the GPU state accessor for compute shader translation.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="state">Current GPU state</param>
        /// <param name="localSizeX">Local group size X of the compute shader</param>
        /// <param name="localSizeY">Local group size Y of the compute shader</param>
        /// <param name="localSizeZ">Local group size Z of the compute shader</param>
        /// <param name="localMemorySize">Local memory size of the compute shader</param>
        /// <param name="sharedMemorySize">Shared memory size of the compute shader</param>
        public GpuAccessor(
            GpuContext context,
            GpuChannel channel,
            GpuAccessorState state,
            int localSizeX,
            int localSizeY,
            int localSizeZ,
            int localMemorySize,
            int sharedMemorySize) : base(context)
        {
            _channel = channel;
            _state = state;
            _compute = true;
            _localSizeX = localSizeX;
            _localSizeY = localSizeY;
            _localSizeZ = localSizeZ;
            _localMemorySize = localMemorySize;
            _sharedMemorySize = sharedMemorySize;
        }

        /// <summary>
        /// Reads data from the constant buffer 1.
        /// </summary>
        /// <param name="offset">Offset in bytes to read from</param>
        /// <returns>Value at the given offset</returns>
        public uint ConstantBuffer1Read(int offset)
        {
            if (Cb1DataSize < offset + 4)
            {
                Cb1DataSize = offset + 4;
            }

            ulong baseAddress = _compute
                ? _channel.BufferManager.GetComputeUniformBufferAddress(1)
                : _channel.BufferManager.GetGraphicsUniformBufferAddress(_stageIndex, 1);

            return _channel.MemoryManager.Physical.Read<uint>(baseAddress + (ulong)offset);
        }

        /// <summary>
        /// Prints a log message.
        /// </summary>
        /// <param name="message">Message to print</param>
        public void Log(string message)
        {
            Logger.Warning?.Print(LogClass.Gpu, $"Shader translator: {message}");
        }

        /// <summary>
        /// Gets a span of the specified memory location, containing shader code.
        /// </summary>
        /// <param name="address">GPU virtual address of the data</param>
        /// <param name="minimumSize">Minimum size that the returned span may have</param>
        /// <returns>Span of the memory location</returns>
        public override ReadOnlySpan<ulong> GetCode(ulong address, int minimumSize)
        {
            int size = Math.Max(minimumSize, 0x1000 - (int)(address & 0xfff));
            return MemoryMarshal.Cast<byte, ulong>(_channel.MemoryManager.GetSpan(address, size));
        }

        public int QueryBindingConstantBuffer(int index)
        {
            return 1 + GetStageIndex() * 18 + index;
        }

        public int QueryBindingStorageBuffer(int index)
        {
            return GetStageIndex() * 16 + index;
        }

        public int QueryBindingTexture(int index)
        {
            return GetStageIndex() * 32 + index;
        }

        public int QueryBindingImage(int index)
        {
            return GetStageIndexForImage() * 8 + index;
        }

        private int GetStageIndex()
        {
            return _stageIndex switch
            {
                0 => 0,
                4 => 1,
                3 => 2,
                1 => 3,
                2 => 4,
                _ => 0
            };
        }

        private int GetStageIndexForImage()
        {
            return _stageIndex switch
            {
                0 => 1,
                4 => 0,
                3 => 2,
                1 => 3,
                2 => 4,
                _ => 0
            };
        }

        /// <summary>
        /// Queries Local Size X for compute shaders.
        /// </summary>
        /// <returns>Local Size X</returns>
        public int QueryComputeLocalSizeX() => _localSizeX;

        /// <summary>
        /// Queries Local Size Y for compute shaders.
        /// </summary>
        /// <returns>Local Size Y</returns>
        public int QueryComputeLocalSizeY() => _localSizeY;

        /// <summary>
        /// Queries Local Size Z for compute shaders.
        /// </summary>
        /// <returns>Local Size Z</returns>
        public int QueryComputeLocalSizeZ() => _localSizeZ;

        /// <summary>
        /// Queries Local Memory size in bytes for compute shaders.
        /// </summary>
        /// <returns>Local Memory size in bytes</returns>
        public int QueryComputeLocalMemorySize() => _localMemorySize;

        /// <summary>
        /// Queries Shared Memory size in bytes for compute shaders.
        /// </summary>
        /// <returns>Shared Memory size in bytes</returns>
        public int QueryComputeSharedMemorySize() => _sharedMemorySize;

        /// <summary>
        /// Queries Constant Buffer usage information.
        /// </summary>
        /// <returns>A mask where each bit set indicates a bound constant buffer</returns>
        public uint QueryConstantBufferUse()
        {
            return _compute
                ? _channel.BufferManager.GetComputeUniformBufferUseMask()
                : _channel.BufferManager.GetGraphicsUniformBufferUseMask(_stageIndex);
        }

        /// <summary>
        /// Queries current primitive topology for geometry shaders.
        /// </summary>
        /// <returns>Current primitive topology</returns>
        public InputTopology QueryPrimitiveTopology()
        {
            return _state.ChannelState.Topology switch
            {
                PrimitiveTopology.Points => InputTopology.Points,
                PrimitiveTopology.Lines or
                PrimitiveTopology.LineLoop or
                PrimitiveTopology.LineStrip => InputTopology.Lines,
                PrimitiveTopology.LinesAdjacency or
                PrimitiveTopology.LineStripAdjacency => InputTopology.LinesAdjacency,
                PrimitiveTopology.Triangles or
                PrimitiveTopology.TriangleStrip or
                PrimitiveTopology.TriangleFan => InputTopology.Triangles,
                PrimitiveTopology.TrianglesAdjacency or
                PrimitiveTopology.TriangleStripAdjacency => InputTopology.TrianglesAdjacency,
                PrimitiveTopology.Patches => _state.ChannelState.TessellationMode.UnpackPatchType() == TessPatchType.Isolines
                    ? InputTopology.Lines
                    : InputTopology.Triangles,
                _ => InputTopology.Points
            };
        }

        /// <summary>
        /// Queries the tessellation evaluation shader primitive winding order.
        /// </summary>
        /// <returns>True if the primitive winding order is clockwise, false if counter-clockwise</returns>
        public bool QueryTessCw() => _state.ChannelState.TessellationMode.UnpackCw();

        /// <summary>
        /// Queries the tessellation evaluation shader abstract patch type.
        /// </summary>
        /// <returns>Abstract patch type</returns>
        public TessPatchType QueryTessPatchType() => _state.ChannelState.TessellationMode.UnpackPatchType();

        /// <summary>
        /// Queries the tessellation evaluation shader spacing between tessellated vertices of the patch.
        /// </summary>
        /// <returns>Spacing between tessellated vertices of the patch</returns>
        public TessSpacing QueryTessSpacing() => _state.ChannelState.TessellationMode.UnpackSpacing();

        /// <summary>
        /// Gets the texture descriptor for a given texture on the pool.
        /// </summary>
        /// <param name="handle">Index of the texture (this is the word offset of the handle in the constant buffer)</param>
        /// <param name="cbufSlot">Constant buffer slot for the texture handle</param>
        /// <returns>Texture descriptor</returns>
        public override Image.ITextureDescriptor GetTextureDescriptor(int handle, int cbufSlot)
        {
            if (_compute)
            {
                return _channel.TextureManager.GetComputeTextureDescriptor(
                    _state.ChannelState.TexturePoolGpuVa,
                    _state.ChannelState.TextureBufferIndex,
                    _state.ChannelState.TexturePoolMaximumId,
                    handle,
                    cbufSlot);
            }
            else
            {
                return _channel.TextureManager.GetGraphicsTextureDescriptor(
                    _state.ChannelState.TexturePoolGpuVa,
                    _state.ChannelState.TextureBufferIndex,
                    _state.ChannelState.TexturePoolMaximumId,
                    _stageIndex,
                    handle,
                    cbufSlot);
            }
        }

        public override bool QueryIsTextureRectangle(int handle, int cbufSlot = -1)
        {
            var descriptor = GetTextureDescriptor(handle, cbufSlot);
            _state.SpecializationState?.RecordTextureCoordNormalized(_stageIndex, handle, cbufSlot, descriptor.UnpackTextureCoordNormalized());
            return QueryIsTextureRectangle(descriptor);
        }

        /// <summary>
        /// Queries transform feedback enable state.
        /// </summary>
        /// <returns>True if the shader uses transform feedback, false otherwise</returns>
        public bool QueryTransformFeedbackEnabled()
        {
            return _state.ChannelState.TransformFeedbackDescriptors != null;
        }

        /// <summary>
        /// Queries the varying locations that should be written to the transform feedback buffer.
        /// </summary>
        /// <param name="bufferIndex">Index of the transform feedback buffer</param>
        /// <returns>Varying locations for the specified buffer</returns>
        public ReadOnlySpan<byte> QueryTransformFeedbackVaryingLocations(int bufferIndex)
        {
            return _state.ChannelState.TransformFeedbackDescriptors[bufferIndex].VaryingLocations;
        }

        /// <summary>
        /// Queries the stride (in bytes) of the per vertex data written into the transform feedback buffer.
        /// </summary>
        /// <param name="bufferIndex">Index of the transform feedback buffer</param>
        /// <returns>Stride for the specified buffer</returns>
        public int QueryTransformFeedbackStride(int bufferIndex)
        {
            return _state.ChannelState.TransformFeedbackDescriptors[bufferIndex].Stride;
        }

        /// <summary>
        /// Queries if host state forces early depth testing.
        /// </summary>
        /// <returns>True if early depth testing is forced</returns>
        public bool QueryEarlyZForce()
        {
            return _state.ChannelState.EarlyZForce;
        }
    }
}
