// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.ResponseCaching
{
    internal class ResponseCachingStream : Stream, IHttpResponseBodyFeature
    {
        private readonly IHttpResponseBodyFeature _innerBody;
        private readonly long _maxBufferSize;
        private readonly int _segmentSize;
        private readonly SegmentWriteStream _segmentWriteStream;
        private readonly Action _startResponseCallback;
        private PipeWriter _pipeAdapter = null;

        internal ResponseCachingStream(IHttpResponseBodyFeature innerBody, long maxBufferSize, int segmentSize, Action startResponseCallback)
        {
            _innerBody = innerBody;
            _maxBufferSize = maxBufferSize;
            _segmentSize = segmentSize;
            _startResponseCallback = startResponseCallback;
            _segmentWriteStream = new SegmentWriteStream(_segmentSize);
        }

        internal bool BufferingEnabled { get; private set; } = true;

        public override bool CanRead => _innerBody.Stream.CanRead;

        public override bool CanSeek => _innerBody.Stream.CanSeek;

        public override bool CanWrite => _innerBody.Stream.CanWrite;

        public override long Length => _innerBody.Stream.Length;

        public override long Position
        {
            get { return _innerBody.Stream.Position; }
            set
            {
                DisableBuffering();
                _innerBody.Stream.Position = value;
            }
        }

        public Stream Stream => this;

        public PipeWriter Writer
        {
            get
            {
                if (_pipeAdapter == null)
                {
                    _pipeAdapter = PipeWriter.Create(Stream, new StreamPipeWriterOptions(leaveOpen: true));
                }

                return _pipeAdapter;
            }
        }

        internal CachedResponseBody GetCachedResponseBody()
        {
            if (!BufferingEnabled)
            {
                throw new InvalidOperationException("Buffer stream cannot be retrieved since buffering is disabled.");
            }
            return new CachedResponseBody(_segmentWriteStream.GetSegments(), _segmentWriteStream.Length);
        }

        internal void DisableBuffering()
        {
            BufferingEnabled = false;
            _segmentWriteStream.Dispose();
        }

        public override void SetLength(long value)
        {
            DisableBuffering();
            _innerBody.Stream.SetLength(value);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            DisableBuffering();
            return _innerBody.Stream.Seek(offset, origin);
        }

        public override void Flush()
        {
            try
            {
                _startResponseCallback();
                _innerBody.Stream.Flush();
            }
            catch
            {
                DisableBuffering();
                throw;
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                _startResponseCallback();
                await _innerBody.Stream.FlushAsync();
            }
            catch
            {
                DisableBuffering();
                throw;
            }
        }

        // Underlying stream is write-only, no need to override other read related methods
        public override int Read(byte[] buffer, int offset, int count)
            => _innerBody.Stream.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                _startResponseCallback();
                _innerBody.Stream.Write(buffer, offset, count);
            }
            catch
            {
                DisableBuffering();
                throw;
            }

            if (BufferingEnabled)
            {
                if (_segmentWriteStream.Length + count > _maxBufferSize)
                {
                    DisableBuffering();
                }
                else
                {
                    _segmentWriteStream.Write(buffer, offset, count);
                }
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                _startResponseCallback();
                await _innerBody.Stream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            catch
            {
                DisableBuffering();
                throw;
            }

            if (BufferingEnabled)
            {
                if (_segmentWriteStream.Length + count > _maxBufferSize)
                {
                    DisableBuffering();
                }
                else
                {
                    await _segmentWriteStream.WriteAsync(buffer, offset, count, cancellationToken);
                }
            }
        }

        public override void WriteByte(byte value)
        {
            try
            {
                _innerBody.Stream.WriteByte(value);
            }
            catch
            {
                DisableBuffering();
                throw;
            }

            if (BufferingEnabled)
            {
                if (_segmentWriteStream.Length + 1 > _maxBufferSize)
                {
                    DisableBuffering();
                }
                else
                {
                    _segmentWriteStream.WriteByte(value);
                }
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return StreamUtilities.ToIAsyncResult(WriteAsync(buffer, offset, count), callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult == null)
            {
                throw new ArgumentNullException(nameof(asyncResult));
            }
            ((Task)asyncResult).GetAwaiter().GetResult();
        }

        void IHttpResponseBodyFeature.DisableBuffering()
        {
            _innerBody.DisableBuffering();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _startResponseCallback();
                await _innerBody.Stream.FlushAsync();
            }
            catch
            {
                DisableBuffering();
                throw;
            }
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
        {
            return _innerBody.SendFileAsync(path, offset, count, cancellationToken);
        }

        public Task CompleteAsync()
        {
            // TODO
            return _innerBody.CompleteAsync();
        }
    }
}
