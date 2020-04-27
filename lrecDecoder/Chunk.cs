using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace lrecDecoder
{
    class Flags
    {
        public const int
        FLAG_ENCRYPT = 0x1000,
        FLAG_COMPRESSED = 0x2,
        FLAG_BUFFER_ENCRYPTED = 0x20,
        FLAG_UPDATE = 0x100,
        FLAG_HOST = 0x4, // 4
        FLAG_ARCHIVE = 0x40, // 64
        FLAG_RECORDED = 0x80, // 128
        FLAG_INCREMENTAL = 0x200, //512
        FLAG_EMBEDDED = 0x400, // 1024
        FLAG_PADDED = 0x2000 // 8192
        ;

        public bool Encrypted { get; private set; }
        public bool BufferEncrypted { get; private set; }
        public bool Compressed { get; private set; }
        public bool IsUpdate { get; private set; }
        public bool IsHost { get; private set; }
        public bool IsArchive { get; private set; }
        public bool IsRecorded { get; private set; }
        public bool IsIncremental { get; private set; }
        public bool IsEmbedded { get; private set; }
        public bool IsPadded { get; private set; }
        public Flags(int flagInt32)
        {
            Compressed = ((flagInt32 & FLAG_COMPRESSED) == 2);
            Encrypted = !((flagInt32 & FLAG_ENCRYPT) != 4096);
            BufferEncrypted = !((flagInt32 & FLAG_BUFFER_ENCRYPTED) != 32);
            IsUpdate = ((flagInt32 & FLAG_UPDATE) == 256);
            IsHost = ((flagInt32 & FLAG_HOST) == 4);
            IsArchive = ((flagInt32 & FLAG_ARCHIVE) == 64);
            IsRecorded = ((flagInt32 & FLAG_RECORDED) == 128);
            IsIncremental = ((flagInt32 & FLAG_INCREMENTAL) == 512);
            IsEmbedded = ((flagInt32 & FLAG_EMBEDDED) == 1024);
            IsPadded = !((flagInt32 & FLAG_PADDED) != 8192);
        }

        public override string ToString()
        {
            return string.Format(
                  "Compressed: {0}\n"
                + "Encrypted: {1}\n"
                + "BufferEncrypted: {2}\n"
                + "IsUpdate: {3}\n"
                + "IsHost: {4}\n"
                + "IsArchive: {5}\n"
                + "IsRecorded: {6}\n"
                + "IsIncremental: {7}\n"
                + "IsEmbedded: {8}\n"
                + "IsPadded: {9}\n"
                , new object[] {Compressed.ToString() , Encrypted.ToString() ,BufferEncrypted.ToString() ,IsUpdate.ToString() ,IsHost.ToString() ,
                    IsArchive.ToString() ,IsRecorded.ToString() ,IsIncremental.ToString(),IsEmbedded.ToString(),IsPadded.ToString()}
                );

        }


    }
    class Header
    {


        public int Size { get; private set; }
        public int Control { get; private set; }
        public int InflatedSize { get; private set; }
        public int Request { get; private set; }
        public int Route { get; private set; }
        public Flags Flags { get; private set; }
        public Header(byte[] header)
        {
            Size = BC.bytesToInt(header, 2, 3) - 11;
            Control = BC.bytesToInt(header, 5, 2);
            InflatedSize = BC.bytesToInt(header, 13, 3);
            Request = BC.bytesToInt(header, 11, 2);
            Route = BC.bytesToInt(header, 9, 2);
            Flags = new Flags(BC.bytesToInt(header, 7, 2));
        }
        public override string ToString()
        {
            return string.Format("Size: {0}\nControl: {1}\nInflatedSize: {2}\nRequest: {3}\nRoute: {4}\nFlags: {5}\n"
                , new object[] { Size, Control, InflatedSize, Request, Route, Flags.ToString() });
        }
    }
    class Chunk
    {
        public long TimeStamp { get; private set; }
        public Header Header { get; private set; }
        public byte[] Buffer { get; private set; }
        public int Offset => sz;
        int sz = 0;
        public Chunk(byte[] data, int Start = 0)
        {
            int start = Start;
            if (data[0] == 0)
            {
                byte[] long8Bit = new byte[8];
                Array.Copy(data, start, long8Bit, 0, 8);
                start += 8;
                TimeStamp = BC.bytesToInt(long8Bit, 0, 4) + BC.bytesToInt(long8Bit, 4, 4);
            }

            byte[] head = new byte[16];
            Array.Copy(data, start, head, 0, 16);

            Header = new Header(head);

            byte[] buf = null;
            bool isSound = false;
            int off = 0;
            if (Header.Flags.IsEmbedded)
            {
                int x1 = BC.bytesToInt(data, start + 16, 2);
                int x2 = BC.bytesToInt(data, start + 18, 2);
                off = x2 + 36;

                byte[] head2 = new byte[16];
                Array.Copy(data, start + 16 + x2, head2, 0, 16);
                Header h = new Header(head2);
                byte indf = data[start + off];

                if (h.Size - 5 == 256)
                {
                    isSound = true;
                    buf = new byte[h.Size - 5];
                    Array.Copy(data, start + off + 1, buf, 0, h.Size - 5);
                }
                else
                {
                    buf = new byte[Header.Size];
                    Array.Copy(data, start + 16, buf, 0, Header.Size);
                }
            }
            else
            {
                buf = new byte[Header.Size];
                Array.Copy(data, start + 16, buf, 0, Header.Size);
            }

            Buffer = buf;

            sz = isSound ? start + off + 1 + buf.Length : start + 16 + buf.Length;
        }
    }
}
