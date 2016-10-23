﻿using System;
using System.Buffers;

namespace Channels
{
    /// <summary>
    /// Represents a buffer that is completely owned by this object.
    /// </summary>
    public class OwnedBuffer : ReferenceCountedBuffer
    {
        private byte[] _buffer;

        /// <summary>
        /// Create a new instance of <see cref="OwnedBuffer"/> that spans the array provided.
        /// </summary>
        public OwnedBuffer(byte[] buffer)
        {
            _buffer = buffer;
        }

        protected override Span<byte> GetSpanCore()
        {
            return _buffer;
        }

        protected override void DisposeCore()
        {
            // No need, the GC can handle it.
        }

        protected override bool TryGetArrayCore(out ArraySegment<byte> buffer)
        {
            buffer = new ArraySegment<byte>(_buffer);
            return true;
        }

        protected override unsafe bool TryGetPointerCore(out void* pointer)
        {
            pointer = null;
            return false;
        }
    }
}