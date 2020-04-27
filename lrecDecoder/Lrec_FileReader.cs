using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace lrecDecoder
{
    struct ImageFrame
    {
        public Bitmap image;
        public Rectangle rect;
        public long time;
        public object holder;
        public void Dispose()
        {
            image.Dispose();
        }
    }
    struct ImageFrameHeader
    {
        public int Width;
        public int Height;
        public int X;
        public int Y;
        public int Length;
        public int BitsPerPixel;
        public int Stride;
        public PixelFormat PixelFormat;
        public ImageFrameHeader(byte[] data)
        {
            X = BC.bytesToInt(data, 1, 4);
            Y = BC.bytesToInt(data, 5, 4);
            Width = BC.bytesToInt(data, 9, 4);
            Height = BC.bytesToInt(data, 13, 4);
            Length = BC.bytesToInt(data, 17, 4);
            BitsPerPixel = Length / (Width * Height);
            Stride = Width * BitsPerPixel;
            switch (BitsPerPixel)
            {
                case 3:
                    PixelFormat = PixelFormat.Format24bppRgb;
                    break;
                case 2:
                    PixelFormat = PixelFormat.Format16bppRgb555;
                    break;
                default:
                    PixelFormat = PixelFormat.Format8bppIndexed;
                    break;

            }
        }
        public Rectangle GetRectangle() => new Rectangle(X, Y, Width, Height);

    }
    class Lrec_FileReader : IDisposable
    {
        public delegate void IP_Callback(ImageFrame[] frames);
        public delegate void AP_Callback(byte[] audiobuffer);

        public event IP_Callback ImageProcess;
        public event AP_Callback AudioProcess;
        public int BufferedFramesCount { get => _bufferedVid.Count; }
        public bool Playing { get => _is_playing; }
        public long[] TimeStamps { get => _timestamps.ToArray(); }

        const int MAX_BUFFERED_FRAMES = 10;

        Chunk _current_chunk;
        Color[] _palette = null;
        FileStream _fs;
        List<long> _timestamps;
        List<long> _audio_offset;
        List<ImageFrame[]> _bufferedVid;
        Thread _readThread;
        List<Chunk> _chunks;

        string _filename;
        long _current_timestamp = 0;
        byte[] _data = null;
        int _current_offset = 0;
        int _current_chunk_index = 0;
        string _path;
        byte[] _ts_header = null;
        bool _is_playing = false;

        public Lrec_FileReader(string fn, string path)
        {
            _filename = fn;
            _data = File.ReadAllBytes(_filename);
            _timestamps = new List<long>();
            _audio_offset = new List<long>();
            _path = @"tmp\";
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);
            _ts_header = File.ReadAllBytes("tsheader.bin");
            _fs = new FileStream(_path + "ts.wav", FileMode.OpenOrCreate);
            _bufferedVid = new List<ImageFrame[]>();
            _chunks = new List<Chunk>();
            init_all();
            _readThread = new Thread(read_thread);
        }
        void read_thread()
        {
            while (true)
            {
                if (!_is_playing) break;
                read_frame(null);
                Thread.Sleep(10);
            }
        }
        void init_all()
        {
            _fs.Write(_ts_header, 0, _ts_header.Length);
            _fs.Flush();
            int off = 0;
            while (off < _data.Length)
            {
                Chunk x = new Chunk(_data, off);


                if (x.Header.Control == 3)
                {
                    _timestamps.Add(x.TimeStamp);
                    _chunks.Add(x);
                }
                if (x.Header.Flags.IsEmbedded && x.Header.Control == 2 && x.Buffer[0] != 1)
                {
                    _audio_offset.Add(_fs.Position + (x.Buffer.Length * 15));
                    _fs.Write(x.Buffer, 0, x.Buffer.Length);
                    _fs.Flush();
                }

                off = x.Offset;
            }
            _fs.Close();
            convert_using_ffmpeg();
        }

        public ImageFrame[] GetBufferedNextFrame()
        {
            if (_bufferedVid.Count > 0)
            {
                //ImageProcess?.Invoke(_bufferedVid[0]);
                //long xx = _bufferedVid[0][0].time / _bufferedVid[0].Length;
                var xx = _bufferedVid[0];
                _bufferedVid.RemoveAt(0);
                return xx;
            }
            return null;
        }

        void convert_using_ffmpeg()
        {
            if (File.Exists(_path + "wav.wav")) File.Delete(_path + "wav.wav");
            var ffmpeg = "ffmpeg.exe";
            Process x = new Process();
            x.StartInfo.FileName = ffmpeg;
            x.StartInfo.Arguments = "-i " + @"tmp\ts.wav -ar 8000 tmp\wav.wav";
            x.OutputDataReceived += X_OutputDataReceived;
            x.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            x.Start();
            x.WaitForExit();
        }

        private void X_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.Data);
        }

        void read_frame(object o)
        {
            if (_current_offset != _data.Length)
            {
                if (_bufferedVid.Count == MAX_BUFFERED_FRAMES) return;
                _current_chunk = _chunks[_current_chunk_index];
                _current_chunk_index++;
                if (_current_chunk.Header.Control == 3 && _current_chunk.Header.Flags.Compressed)
                {
                    _bufferedVid.Add(read_frame_image(_current_chunk));
                    Console.WriteLine("Buffering ..." + _bufferedVid.Count);
                }
                else if (_current_chunk.Buffer[0] == 2 && _palette == null)
                {
                    Console.WriteLine("Buffering palette...");
                    _palette = BC.bytesToPallete(_current_chunk.Buffer);
                }
            }
        }
        ImageFrame[] read_frame_image(Chunk chunk)
        {
            byte[] imageBuffer = null;
            if (chunk.Header.Flags.Compressed)
            {
                using (MemoryStream ms = new MemoryStream(chunk.Buffer, 2, chunk.Buffer.Length - 4))
                {
                    using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                    {
                        imageBuffer = new byte[chunk.Header.InflatedSize];
                        ds.Read(imageBuffer, 0, imageBuffer.Length);
                    }
                }
            }
            else
            {
                return null;
            }
            byte[] head = new byte[21];
            Array.Copy(imageBuffer, 0, head, 0, 21);
            ImageFrameHeader header = new ImageFrameHeader(head);

            object holder = null;
            long time = 0;
            if (_current_chunk_index < _timestamps.Count - 1)
            {
                holder = _timestamps[_current_chunk_index] + "ms";
                time = _timestamps[_current_chunk_index + 1] - _timestamps[_current_chunk_index];
            }


            byte[] newBuffer = new byte[imageBuffer.Length - 21];
            Array.Copy(imageBuffer, 21, newBuffer, 0, imageBuffer.Length - 21);


            if (newBuffer.Length > header.Length)
            {
                List<ImageFrame> frames = new List<ImageFrame>();

                do
                {
                    byte[] frame = new byte[header.Length];
                    Array.Copy(newBuffer, 0, frame, 0, frame.Length);
                    frames.Add(new ImageFrame()
                    {
                        image = BC.BuildImage(frame, header.Width, header.Height, header.Stride, header.PixelFormat, _palette, Color.White)
                        ,
                        rect = header.GetRectangle()
                        ,
                        holder = holder
                        ,
                        time = time
                    });
                    byte[] tmp = new byte[newBuffer.Length - header.Length];
                    Array.Copy(newBuffer, header.Length, tmp, 0, tmp.Length);
                    Array.Copy(tmp, 0, head, 0, 21);
                    int oldLen = header.Length;
                    header = new ImageFrameHeader(tmp);
                    int tmpLen = tmp.Length - 21;
                    tmp = new byte[tmpLen];
                    Array.Copy(newBuffer, oldLen + 21, tmp, 0, tmp.Length);
                    newBuffer = tmp;
                } while (newBuffer.Length > header.Length);

                //File.WriteAllBytes("1.bin", newBuffer);
                return frames.ToArray();
            }
            else
            {
                return new ImageFrame[]
                {
                    new ImageFrame()
                    {
                        image = BC.BuildImage(newBuffer, header.Width, header.Height, header.Stride, header.PixelFormat, _palette, Color.White)
                        ,
                        rect = header.GetRectangle()
                        ,
                        holder = holder
                        ,
                        time = time
                    }
                };
            }
        }
        byte[] read_frame_audio(byte[] data)
        {



            return null;
        }
        public void Stop()
        {
            _is_playing = false;
            _readThread.Join();
            _current_offset = 0;
            _current_chunk_index = 0;
            _bufferedVid.Clear();
        }
        public void Pause()
        {
            _is_playing = false;
            _readThread.Join();
        }
        public void Start()
        {
            _is_playing = true;
            _readThread = new Thread(read_thread);
            _readThread.Start();
        }
        public void Dispose()
        {
            _is_playing = false;
            _readThread.Join();
            _current_chunk = null;
            _bufferedVid.Clear();
            _timestamps.Clear();
            _audio_offset.Clear();
            _fs.Close();
            _fs.Dispose();
            _fs = null;
        }
    }
}
