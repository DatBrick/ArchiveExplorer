﻿using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using Archive.Common;

namespace Archive.Web
{
    public class HttpStream : Stream
    {
        protected long position_;
        protected long? length_;
        protected byte[] data_;

        public Uri Path { get; }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }

        public override long Length
        {
            get
            {
                if (length_.HasValue)
                {
                    return length_.Value;
                }
                else if (data_ != null)
                {
                    return data_.Length;
                }

                throw new NotSupportedException("HTTP Stream does not support HEAD");
            }
        }

        public override long Position
        {
            get
            {
                return position_;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        protected int ReadCachedData(byte[] buffer, int offset, int count)
        {
            if ((position_ + count) > data_.Length)
            {
                count = data_.Length - (int)position_;
            }

            Array.Copy(data_, position_, buffer, offset, count);

            position_ += count;

            return count;
        }

        public HttpStream(Uri path)
        {
            Path = path;
            length_ = null;
            position_ = 0;

            WebHeaderCollection headers = null;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(path);

                request.Method = "HEAD";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    headers = response.Headers;

                    Path = response.ResponseUri;
                    length_ = response.ContentLength;
                }

                // TODO: Send "OPTIONS" Request?
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse) ex.Response;
                
                if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    headers = ex.Response.Headers;

                    Path = ex.Response.ResponseUri;
                    length_ = null;
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                var allow = headers?["Allow"];
                CanRead = allow?.Contains("GET") ?? true;
                CanWrite = allow?.Contains("PUT") ?? true;

                var accept = headers?["Accept-Ranges"];
                CanSeek = accept?.Contains("bytes") ?? false;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (CanSeek)
            {
                var request = (HttpWebRequest)WebRequest.Create(Path);

                request.Method = "GET";
                request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.CacheIfAvailable);
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

                request.AddRange(position_, position_ + count - 1);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    int total = 0;

                    while ((total < count) && (total < response.ContentLength))
                    {
                        int read = stream.Read(buffer, offset + total, count - total);

                        if (read > 0)
                        {
                            total += read;
                        }
                        else
                        {
                            break;
                        }
                    }

                    position_ += total;

                    return total;
                }
            }
            else
            {
                if (data_ == null)
                {
                    var request = (HttpWebRequest)WebRequest.Create(Path);

                    request.Method = "GET";
                    request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.CacheIfAvailable);
                    request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    {
                        data_ = Utils.ReadAllBytes(stream);
                    }
                }

                return ReadCachedData(buffer, offset, count);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    break;

                case SeekOrigin.Current:
                    offset += position_;
                    break;

                case SeekOrigin.End:
                    offset += Length;
                    break;
            }

            if (offset > Length)
            {
                offset = Length;
            }
            else if (offset < 0)
            {
                offset = 0;
            }

            position_ = offset;

            return position_;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var request = (HttpWebRequest)WebRequest.Create(Path);

            request.Method = "PUT";
            request.ContentLength = count;
            request.AllowWriteStreamBuffering = true;
            request.AddRange(position_, position_ + count);

            using (var stream = request.GetRequestStream())
            {
                stream.Write(buffer, offset, count);
            }

            var response = (HttpWebResponse)request.GetResponse();
        }

        public override void Flush()
        {

        }
    }
}