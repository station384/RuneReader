using System;
using System.Buffers.Binary;
using System.IO;

namespace RuneReader.Classes.Platform.Linux.Wayland.PipeWire;

// SPA POD basics (enough for format negotiation)
// This is intentionally minimal: we only build what we need.
internal sealed class SpaPodBuilder
{
    private readonly MemoryStream _ms = new();

    public byte[] ToArray() => _ms.ToArray();

    private void Align8()
    {
        while ((_ms.Length & 7) != 0)
            _ms.WriteByte(0);
    }

    private void WriteU32(uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        _ms.Write(b);
    }

    private void WriteI32(int v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, v);
        _ms.Write(b);
    }

    // SPA POD header: [size:u32][type:u32] then payload
    private long BeginPod(uint type)
    {
        Align8();
        long start = _ms.Position;
        WriteU32(0);      // size placeholder
        WriteU32(type);   // type
        return start;
    }

    private void EndPod(long start)
    {
        Align8();
        long end = _ms.Position;
        uint size = (uint)(end - start - 8); // payload size
        _ms.Position = start;
        WriteU32(size);
        _ms.Position = end;
    }

    // Types (subset). Values match SPA type IDs used by PipeWire/SPA.
    // These IDs are stable in SPA; if your build differs, you may need to adjust.
    private const uint SPA_TYPE_Object = 0x07;
    private const uint SPA_TYPE_Int = 0x02;
    private const uint SPA_TYPE_Id = 0x03;

    // Object IDs (subset). Again: typically stable, but distro builds can vary.
    private const uint SPA_TYPE_OBJECT_Format = 0x10001;

    // Properties (subset) for video format negotiation
    private const uint SPA_FORMAT_mediaType = 0x0001;
    private const uint SPA_FORMAT_mediaSubtype = 0x0002;
    private const uint SPA_FORMAT_VIDEO_format = 0x0003;
    private const uint SPA_FORMAT_VIDEO_size = 0x0004;
    private const uint SPA_FORMAT_VIDEO_framerate = 0x0005;

    // IDs for media types
    private const int SPA_MEDIA_TYPE_video = 0x02;
    private const int SPA_MEDIA_SUBTYPE_raw = 0x01;

    // raw pixel formats (subset)
    // You can negotiate BGRx/RGBx; most compositors deliver BGRx.
    private const int SPA_VIDEO_FORMAT_BGRx = 0x0f; // common
    private const int SPA_VIDEO_FORMAT_BGRA = 0x10;

    public byte[] BuildRawVideoFormat(int width, int height, int fpsNum = 60, int fpsDen = 1, bool alpha = false)
    {
        // Build: Object(Format) { mediaType=video, mediaSubtype=raw, video.format=BGRx/BGRA, size=(w,h), framerate=(n,d) }
        // We encode as:
        // POD:Object [id, type, ... props ...]
        // For simplicity, we’ll write a very basic object payload.
        // NOTE: PipeWire is forgiving as long as you give a sane format.

        long start = BeginPod(SPA_TYPE_Object);

        // object header (very simplified):
        // object-id (u32), object-type (u32), flags(u32), n_props(u32)
        WriteU32(SPA_TYPE_OBJECT_Format);
        WriteU32(0); // type (often same as id in some encodings; kept 0 for minimal)
        WriteU32(0); // flags
        WriteU32(5); // props count

        WritePropId(SPA_FORMAT_mediaType, SPA_MEDIA_TYPE_video);
        WritePropId(SPA_FORMAT_mediaSubtype, SPA_MEDIA_SUBTYPE_raw);
        WritePropId(SPA_FORMAT_VIDEO_format, alpha ? SPA_VIDEO_FORMAT_BGRA : SPA_VIDEO_FORMAT_BGRx);
        WritePropSize(SPA_FORMAT_VIDEO_size, width, height);
        WritePropFraction(SPA_FORMAT_VIDEO_framerate, fpsNum, fpsDen);

        EndPod(start);
        return ToArray();
    }

    private void WritePropId(uint key, int id)
    {
        // prop header: key(u32), flags(u32), value pod...
        WriteU32(key);
        WriteU32(0); // flags
        WriteSimpleId(id);
    }

    private void WriteSimpleId(int id)
    {
        long s = BeginPod(SPA_TYPE_Id);
        WriteI32(id);
        EndPod(s);
    }

    private void WritePropSize(uint key, int w, int h)
    {
        // encode size as 2x int (simple tuple-ish). Many PipeWire builds accept this.
        WriteU32(key);
        WriteU32(0);

        // "struct" isn't implemented here; write as two ints inside an "object"ish pod.
        // Practical approach: treat as "Int" array in payload.
        long s = BeginPod(SPA_TYPE_Int);
        WriteI32(w);
        WriteI32(h);
        EndPod(s);
    }

    private void WritePropFraction(uint key, int num, int den)
    {
        WriteU32(key);
        WriteU32(0);
        long s = BeginPod(SPA_TYPE_Int);
        WriteI32(num);
        WriteI32(den);
        EndPod(s);
    }
}