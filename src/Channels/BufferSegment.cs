﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Text;

namespace Channels
{
    // TODO: Pool segments
    internal class BufferSegment : IDisposable
    {
        /// <summary>
        /// The buffer being tracked
        /// </summary>
        public OwnedMemory<byte> Buffer;

        /// <summary>
        /// The Start represents the offset into Array where the range of "active" bytes begins. At the point when the block is leased
        /// the Start is guaranteed to be equal to 0. The value of Start may be assigned anywhere between 0 and
        /// Buffer.Length, and must be equal to or less than End.
        /// </summary>
        public int Start;

        /// <summary>
        /// The End represents the offset into Array where the range of "active" bytes ends. At the point when the block is leased
        /// the End is guaranteed to be equal to Start. The value of Start may be assigned anywhere between 0 and
        /// Buffer.Length, and must be equal to or less than End.
        /// </summary>
        public int End;

        /// <summary>
        /// Reference to the next block of data when the overall "active" bytes spans multiple blocks. At the point when the block is
        /// leased Next is guaranteed to be null. Start, End, and Next are used together in order to create a linked-list of discontiguous 
        /// working memory. The "active" memory is grown when bytes are copied in, End is increased, and Next is assigned. The "active" 
        /// memory is shrunk when bytes are consumed, Start is increased, and blocks are returned to the pool.
        /// </summary>
        public BufferSegment Next;


        /// <summary>
        /// If true, data should not be written into the backing block after the End offset. Data between start and end should never be modified
        /// since this would break cloning.
        /// </summary>
        public bool ReadOnly;

        /// <summary>
        /// The amount of readable bytes in this segment
        /// </summary>
        public int ReadableBytes => End - Start;

        /// <summary>
        /// The amount of writable bytes in this segment
        /// </summary>
        public int WritableBytes => Buffer.Length - End;


        // Leasing ctor
        public BufferSegment(OwnedMemory<byte> buffer)
        {
            Buffer = buffer;
            Start = 0;
            End = 0;

            Buffer.AddReference();
        }

        // Cloning ctor
        internal BufferSegment(OwnedMemory<byte> buffer, int start, int end)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            ReadOnly = true;

            // For unowned buffers, we need to make a copy here so that the caller can 
            // give up the give this buffer back to the caller
            var unowned = buffer as UnownedBuffer;
            if (unowned != null)
            {
                Buffer = unowned.MakeCopy(start, end - start, out Start, out End);

                // Release the reference to the unowned buffer
                unowned.Release();
            }

            Buffer.AddReference();
        }

        public void Dispose()
        {
            Buffer.Release();

            if (Buffer.ReferenceCount == 0)
            {
                Buffer.Dispose();
            }
        }


        /// <summary>
        /// ToString overridden for debugger convenience. This displays the "active" byte information in this block as ASCII characters.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            var data = Buffer.Memory.Slice(Start, ReadableBytes).Span;

            for (int i = 0; i < ReadableBytes; i++)
            {
                builder.Append((char)data[i]);
            }
            return builder.ToString();
        }

        public static BufferSegment Clone(ReadCursor beginBuffer, ReadCursor endBuffer, out BufferSegment lastSegment)
        {
            var beginOrig = beginBuffer.Segment;
            var endOrig = endBuffer.Segment;

            if (beginOrig == endOrig)
            {
                lastSegment = new BufferSegment(beginOrig.Buffer, beginBuffer.Index, endBuffer.Index);
                return lastSegment;
            }

            var beginClone = new BufferSegment(beginOrig.Buffer, beginBuffer.Index, beginOrig.End);
            var endClone = beginClone;

            beginOrig = beginOrig.Next;

            while (beginOrig != endOrig)
            {
                endClone.Next = new BufferSegment(beginOrig.Buffer, beginOrig.Start, beginOrig.End);

                endClone = endClone.Next;
                beginOrig = beginOrig.Next;
            }

            lastSegment = new BufferSegment(endOrig.Buffer, endOrig.Start, endBuffer.Index);
            endClone.Next = lastSegment;

            return beginClone;
        }
    }
}
