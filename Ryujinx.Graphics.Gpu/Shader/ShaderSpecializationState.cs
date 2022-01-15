using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Threed;
using Ryujinx.Graphics.Gpu.Image;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ryujinx.Graphics.Gpu.Shader
{
    class ShaderSpecializationState
    {
        private enum QueriedStateFlags : byte
        {
            EarlyZForce = 1 << 0,
            PrimitiveTopology = 1 << 1,
            TessellationMode = 1 << 2
        }

        private QueriedStateFlags _queriedState;

        private bool _earlyZForce;
        private PrimitiveTopology _topology;
        private TessMode _tessellationMode;

        private enum QueriedTextureStateFlags : byte
        {
            CoordNormalized = 1 << 0
        }

        private class TextureSpecializationState
        {
            public QueriedTextureStateFlags QueriedFlags;
            public bool CoordNormalized;
        }

        private struct TextureKey : IEquatable<TextureKey>
        {
            public readonly int StageIndex;
            public readonly int Handle;
            public readonly int CbufSlot;

            public TextureKey(int stageIndex, int handle, int cbufSlot)
            {
                StageIndex = stageIndex;
                Handle = handle;
                CbufSlot = cbufSlot;
            }

            public override bool Equals(object obj)
            {
                return obj is TextureKey textureKey && Equals(textureKey);
            }

            public bool Equals(TextureKey other)
            {
                return StageIndex == other.StageIndex && Handle == other.Handle && CbufSlot == other.CbufSlot;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(StageIndex, Handle, CbufSlot);
            }
        }

        private readonly Dictionary<TextureKey, TextureSpecializationState> _textureSpecialization;

        public ShaderSpecializationState()
        {
            _textureSpecialization = new Dictionary<TextureKey, TextureSpecializationState>();
        }

        public void RecordEarlyZForce(bool earlyZForce)
        {
            _earlyZForce = earlyZForce;
            _queriedState |= QueriedStateFlags.EarlyZForce;
        }

        public void RecordPrimitiveTopology(PrimitiveTopology topology)
        {
            _topology = topology;
            _queriedState |= QueriedStateFlags.PrimitiveTopology;
        }

        public void RecordTessellationMode(TessMode tessellationMode)
        {
            _tessellationMode = tessellationMode;
            _queriedState |= QueriedStateFlags.TessellationMode;
        }

        public void RecordTextureCoordNormalized(int stageIndex, int handle, int cbufSlot, bool coordNormalized)
        {
            TextureKey key = new TextureKey(stageIndex, handle, cbufSlot);

            if (!_textureSpecialization.TryGetValue(key, out TextureSpecializationState state))
            {
                _textureSpecialization.Add(key, state = new TextureSpecializationState());
            }

            state.CoordNormalized = coordNormalized;
            state.QueriedFlags |= QueriedTextureStateFlags.CoordNormalized;
        }

        public bool MatchesGraphics(GpuChannel channel, GpuChannelState channelState)
        {
            return Matches(channel, channelState, isCompute: false);
        }

        public bool MatchesCompute(GpuChannel channel, GpuChannelState channelState)
        {
            return Matches(channel, channelState, isCompute: true);
        }

        private bool Matches(GpuChannel channel, GpuChannelState channelState, bool isCompute)
        {
            foreach (var kv in _textureSpecialization)
            {
                TextureKey textureKey = kv.Key;
                TextureSpecializationState specializationState = kv.Value;
                TextureDescriptor descriptor;

                if (isCompute)
                {
                    descriptor = channel.TextureManager.GetComputeTextureDescriptor(
                        channelState.TexturePoolGpuVa,
                        channelState.TextureBufferIndex,
                        channelState.TexturePoolMaximumId,
                        textureKey.Handle,
                        textureKey.CbufSlot);
                }
                else
                {
                    descriptor = channel.TextureManager.GetGraphicsTextureDescriptor(
                        channelState.TexturePoolGpuVa,
                        channelState.TextureBufferIndex,
                        channelState.TexturePoolMaximumId,
                        textureKey.StageIndex,
                        textureKey.Handle,
                        textureKey.CbufSlot);
                }

                if (specializationState.QueriedFlags.HasFlag(QueriedTextureStateFlags.CoordNormalized) &&
                    specializationState.CoordNormalized != descriptor.UnpackTextureCoordNormalized())
                {
                    return false;
                }
            }

            return true;
        }
    }
}