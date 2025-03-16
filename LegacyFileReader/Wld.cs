using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using VisualEQ.Common;
using static System.Console;

namespace VisualEQ.LegacyFileReader
{
    public class Reference<T> where T : class
    {
        readonly Wld Wld;
        public readonly int Ref;

        public Reference(Wld wld, int @ref)
        {
            Wld = wld;
            Ref = wld.PreResolveRef(@ref);
        }

        public string Name => Wld.GetReferenceName(Ref);
        public T Value
        {
            get
            {
                var v = Wld.GetReference(Ref);
                if (v is T tv)
                    return tv;
                WriteLine($"Reference not found? {Ref} '{v}' {typeof(T)}");
                return null;
            }
        }

        public override string ToString()
        {
            switch (Value)
            {
                case null:
                    return $"Ref<{typeof(T).Name}>(unknown)";
                case string s:
                    return $"Ref(\"{s}\")";
                case var val:
                    return $"Ref({(string.IsNullOrEmpty(Name) ? "" : $"{Name}: ")}{val})";
            }
        }
    }

    public class Fragment03
    {
        public string[] Filenames;

        public override string ToString() => $"Fragment03(Filenames=[{string.Join(", ", Filenames.Select(x => $"'{x}'"))}]";
    }

    public class Fragment04
    {
        public uint FrameTime;
        public Reference<Fragment03>[] References;

        public override string ToString() => $"Fragment04(FrameTime={FrameTime}, References={References.Stringify()})";
    }

    public class Fragment05
    {
        public Reference<Fragment04> Reference;

        public override string ToString() => $"Fragment05(Reference={Reference})";
    }

    public class Fragment10
    {
        public (string Name, uint Flags, Reference<Fragment13> PieceTrack, Reference<Fragment2D> Mesh, int[] Children)[] Tracks;
        public Reference<Fragment2D>[] Meshes;

        public override string ToString() => $"Fragment10(Tracks={Tracks.Stringify()}, Meshes={Meshes.Stringify()})";
    }

    public class Fragment11
    {
        public Reference<Fragment10> Reference;

        public override string ToString() => $"Fragment11(Reference={Reference})";
    }

    public class Fragment12
    {
        public uint Flags;
        public (Quaternion Rotation, Vector3 Shift)[] Frames;

        public override string ToString() => $"Fragment12(Flags=0x{Flags:X}, Frames={Frames.Length})";
    }

    public class Fragment13
    {
        public Reference<Fragment12> Reference;
        public uint? MaybeSpeed; // TODO Determine

        public override string ToString() => $"Fragment13(Reference={Reference}, MaybeSpeed={MaybeSpeed?.ToString() ?? "null"})";
    }

    public class Fragment14
    {
        public Reference<string> Magic;
        public Reference<object>[] References;

        public override string ToString() => $"Fragment14(Magic={Magic}, References={References.Stringify()})";
    }

    public class Fragment15
    {
        public Reference<string> Reference;
        public Vector3 Position, Rotation, Scale;

        public override string ToString() => $"Fragment15(Reference={Reference}, Position={Position}, Rotation={Rotation}, Scale={Scale})";
    }

    public class Fragment1B
    {
        public uint? Attenuation;
        public Vector3 Color;

        public override string ToString() => $"Fragment1B(Attenuation={Attenuation}, Color={Color})";
    }

    public class Fragment1C
    {
        public Reference<Fragment1B> Reference;

        public override string ToString() => $"Fragment1C(Reference={Reference})";
    }

    public class Fragment22
    {
        public override string ToString() => $"{GetType().Name} UNIMPLEMENTED";
    }

    public class Fragment28
    {
        public Reference<Fragment1C> Reference;
        public uint Flags;
        public Vector3 Pos;
        public float Radius;

        public override string ToString() => $"Fragment28(Reference={Reference}, Flags=0x{Flags:X}, Pos={Pos}, Radius={Radius})";
    }

    public class Fragment29
    {
        public override string ToString() => $"{GetType().Name} UNIMPLEMENTED";
    }

    public class Fragment2A
    {
        public override string ToString() => $"{GetType().Name} UNIMPLEMENTED";
    }

    public class Fragment2D
    {
        public Reference<Fragment36> Reference;

        public override string ToString() => $"Fragment2D(Reference={Reference})";
    }

    public class Fragment2F
    {
        public override string ToString() => $"{GetType().Name} UNIMPLEMENTED";
    }

    public class Fragment30
    {
        public Reference<Fragment05> Reference;
        public uint Flags;

        public override string ToString() => $"Fragment30(Reference={Reference}, Flags=0x{Flags:X8})";
    }

    public class Fragment31
    {
        public Reference<Fragment30>[] References;

        public override string ToString() => $"Fragment31(References={References.Stringify()})";
    }

    public class Fragment36
    {
        public Reference<Fragment31> TextureListReference;
        public Vector3[] Vertices, Normals;
        public Vector2[] TexCoords;
        public (bool Collidable, uint A, uint B, uint C)[] Polygons;
        public (uint Count, uint Index)[] PolyTexs;
        public (uint Count, uint Index)[] VertBones;

        public override string ToString() => $"Fragment36(Vertices={Vertices.Length}, Polygons={Polygons.Length}, Textures={PolyTexs.Length}, Bones={VertBones.Length})";
    }

    public class Fragment37
    {
        public override string ToString() => $"{GetType().Name} UNIMPLEMENTED";
    }

    public class FragmentIgnored
    {
        public uint Type;

        public override string ToString() => $"Fragment{Type:X02} ignored";
    }

    public class Wld
    {
        static readonly byte[] StringHashKey = { 0x95, 0x3A, 0xC5, 0x2A, 0x95, 0x7A, 0x95, 0x6A };

        public readonly S3D S3D;
        public readonly string Filename;

        readonly Stream Fp;
        readonly BinaryReader Br;
        readonly bool NewFormat;

        readonly string StringHash;

        internal readonly (string Name, object Fragment)[] Fragments;
        readonly Dictionary<string, int> NameIndex = new Dictionary<string, int>();

        public Wld(S3D s3d, string fn)
        {
            S3D = s3d;
            Filename = fn;
            Fp = s3d.Open(fn);
            Br = new BinaryReader(Fp);

            var magic = Br.ReadUInt32();
            Debug.Assert(magic == 0x54503D02);
            NewFormat = Br.ReadUInt32() != 0x00015500;

            var fragCount = Br.ReadUInt32();
            Fp.Position += 8;
            var stringHashSize = Br.ReadUInt32();
            Fp.Position += 4;

            StringHash = ReadDecodeString((int)stringHashSize);

            while (Fp.Position % 4 != 0)
                Fp.Position++;

            Fragments = new (string, object)[fragCount];

            for (var i = 0; i < fragCount; ++i)
            {
                var size = Br.ReadUInt32();
                var type = Br.ReadUInt32();
                var spos = Fp.Position;
                var nr = Br.ReadInt32();
                var name = nr <= 0 && type != 0x35 ? GetString(-nr) : "";

                void Add<T>(T obj)
                {
                    Fragments[i] = (name, obj);
                    NameIndex[name] = i;
                }

                //WriteLine($"Parsing fragment {i}, type 0x{type:X2} with size 0x{size:X} and name '{name}'");

                switch (type)
                {
                    case 0x03: Add(Read03()); break;
                    case 0x04: Add(Read04()); break;
                    case 0x05: Add(Read05()); break;
                    case 0x10: Add(Read10()); break;
                    case 0x11: Add(Read11()); break;
                    case 0x12: Add(Read12()); break;
                    case 0x13: Add(Read13()); break;
                    case 0x14: Add(Read14()); break;
                    case 0x15: Add(Read15()); break;
                    case 0x1B: Add(Read1B()); break;
                    case 0x1C: Add(Read1C()); break;
                    case 0x22: Add(Read22()); break;
                    case 0x28: Add(Read28()); break;
                    case 0x29: Add(Read29()); break;
                    case 0x2A: Add(Read2A()); break;
                    case 0x2D: Add(Read2D()); break;
                    case 0x2F: Add(Read2F()); break;
                    case 0x30: Add(Read30()); break;
                    case 0x31: Add(Read31()); break;
                    case 0x36: Add(Read36()); break;
                    case 0x37: Add(Read37()); break;
                    case 0x08:
                    case 0x09:
                    case 0x16:
                    case 0x21:
                    case 0x26: // TODO: Figure out -- totally unknown
                    case 0x32:
                    case 0x33:
                    case 0x34: // TODO: Figure out -- totally unknown
                    case 0x35:
                        Add(new FragmentIgnored { Type = type });
                        break;
                    default:
                        WriteLine($"Unhandled fragment type 0x{type:X02}");
                        break;
                }
                Debug.Assert(Fp.Position <= spos + size); // Make sure we didn't read past the end of a fragment
                Fp.Position = spos + size;
            }
        }

        public IEnumerable<(string Name, T Fragment)> GetFragments<T>() => Fragments.Where(x => x.Fragment is T).Select(x => (x.Name, (T)x.Fragment));

        public T GetFragment<T>(string name) where T : class =>
            NameIndex.ContainsKey(name) && Fragments[NameIndex[name]].Fragment is T frag ? frag : null;

        Fragment03 Read03() =>
            new Fragment03
            {
                Filenames = (Br.ReadInt32() + 1).Times(() => ReadDecodeString(Br.ReadUInt16()).TrimEnd('\0')).ToArray()
            };

        Fragment04 Read04()
        {
            var flags = Br.ReadUInt32();
            var refCount = Br.ReadInt32();
            if ((flags & (1 << 2)) != 0)
                Br.ReadUInt32();
            var frameTime = (flags & (1 << 3)) != 0 ? Br.ReadUInt32() : 0;
            var refs = refCount.Times(ReadRef<Fragment03>).ToArray();
            return new Fragment04
            {
                FrameTime = frameTime,
                References = refs
            };
        }

        Fragment05 Read05()
        {
            var reference = ReadRef<Fragment04>();
            Br.ReadUInt32();
            return new Fragment05 { Reference = reference };
        }

        Fragment10 Read10()
        {
            var flags = Br.ReadUInt32();
            var trackCount = Br.ReadUInt32();
            var polyAniRef = ReadRef<object>(); // TODO: Should point to Fragment18
            if (flags.HasBit(0))
                for (var i = 0; i < 3; ++i)
                    Br.ReadUInt32();
            if (flags.HasBit(1))
                Br.ReadSingle();
            var tracks = trackCount.Times(() =>
            {
                var name = (string)GetReference(Br.ReadInt32());
                var tflags = Br.ReadUInt32();
                var sref = ReadRef<Fragment13>();
                var mref = ReadRef<Fragment2D>();
                var children = Br.ReadUInt32().Times(() => Br.ReadInt32()).ToArray();

                return (name, tflags, sref, mref, children);
            }).ToArray();

            var meshes = flags.HasBit(9)
                ? Br.ReadUInt32().Times(ReadRef<Fragment2D>).ToArray()
                : tracks.Select(x => x.Item4).ToArray();

            return new Fragment10 { Tracks = tracks, Meshes = meshes };
        }

        Fragment11 Read11() => new Fragment11 { Reference = ReadRef<Fragment10>() };

        Fragment12 Read12() =>
            new Fragment12
            {
                Flags = Br.ReadUInt32(),
                Frames = Br.ReadUInt32().Times(() =>
                {
                    var rotW = (float)Br.ReadInt16();
                    var rotX = (float)Br.ReadInt16();
                    var rotY = (float)Br.ReadInt16();
                    var rotZ = (float)Br.ReadInt16();
                    var shiftX = (float)Br.ReadInt16();
                    var shiftY = (float)Br.ReadInt16();
                    var shiftZ = (float)Br.ReadInt16();
                    var shiftDenom = Br.ReadInt16();

                    return (
                        new Quaternion(rotX / 16384, rotY / 16384, rotZ / 16384, rotW / 16384),
                        new Vector3(shiftX / shiftDenom, shiftY / shiftDenom, shiftZ / shiftDenom)
                    );
                }).ToArray()
            };

        Fragment13 Read13()
        {
            var reference = ReadRef<Fragment12>();
            var flags = Br.ReadUInt32();
            uint? speed = null;
            if ((flags & 1 << 0) != 0)
                speed = Br.ReadUInt32();
            return new Fragment13 { Reference = reference, MaybeSpeed = speed };
        }

        Fragment14 Read14()
        {
            var flags = Br.ReadUInt32();
            var magic = ReadRef<string>();
            var size = Br.ReadUInt32();
            var numRefs = Br.ReadInt32();
            var frag2 = Br.ReadUInt32();
            if ((flags & (1 << 0)) != 0)
                Br.ReadUInt32();
            if ((flags & (1 << 1)) != 0)
                for (var i = 0; i < 7; ++i)
                    Br.ReadUInt32();
            for (var i = 0; i < size; ++i)
            {
                var count = Br.ReadUInt32();
                for (var j = 0; j < count; ++j)
                {
                    Br.ReadUInt32();
                    Br.ReadSingle();
                }
            }

            var refs = numRefs.Times(ReadRef<object>).ToArray();

            // TODO: Read and understand the encoded string that follows.  See wlddoc

            return new Fragment14 { Magic = magic, References = refs };
        }

        Fragment15 Read15()
        {
            var reference = ReadRef<string>();
            Br.ReadUInt32();
            Br.ReadUInt32();
            var pos = Br.ReadVec3();
            var rot = Br.ReadVec3();
            var scale = Br.ReadVec3();

            scale = scale.Z > 0.0001 ? new Vector3(scale.Z) : Vector3.One;
            rot = new Vector3(rot.Z / 256 * MathF.PI, rot.Y / 256 * MathF.PI, rot.X / 256 * MathF.PI);

            return new Fragment15
            {
                Reference = reference,
                Position = pos,
                Rotation = rot,
                Scale = scale
            };
        }

        Fragment1B Read1B()
        {
            var flags = Br.ReadUInt32();
            Br.ReadUInt32();
            uint? attenuation = null;
            Vector3 color;
            if ((flags & (1 << 4)) != 0)
            {
                if ((flags & (1 << 3)) != 0)
                    attenuation = Br.ReadUInt32();
                Br.ReadSingle();
                color = Br.ReadVec3();
            }
            else
                color = new Vector3(Br.ReadSingle());
            return new Fragment1B { Attenuation = attenuation, Color = color };
        }

        Fragment1C Read1C()
        {
            var reference = ReadRef<Fragment1B>();
            Br.ReadUInt32();
            return new Fragment1C { Reference = reference };
        }

        Fragment22 Read22()
        {
            return new Fragment22();
        }

        Fragment28 Read28()
        {
            var reference = ReadRef<Fragment1C>();
            var flags = Br.ReadUInt32();
            return new Fragment28
            {
                Reference = reference,
                Flags = flags,
                Pos = Br.ReadVec3(),
                Radius = Br.ReadSingle()
            };
        }

        Fragment29 Read29()
        {
            return new Fragment29();
        }

        Fragment2A Read2A()
        {
            return new Fragment2A();
        }

        Fragment2D Read2D() => new Fragment2D { Reference = ReadRef<Fragment36>() };

        Fragment2F Read2F()
        {
            return new Fragment2F();
        }

        Fragment30 Read30()
        {
            var existenceFlags = Br.ReadUInt32();
            var texFlags = Br.ReadUInt32();
            Br.ReadUInt32();
            Br.ReadSingle();
            Br.ReadSingle();
            if ((existenceFlags & (1 << 1)) != 0)
            {
                Br.ReadUInt32();
                Br.ReadSingle();
            }

            var reference = ReadRef<Fragment05>();
            return new Fragment30
            {
                Reference = reference,
                Flags = texFlags
            };
        }

        Fragment31 Read31()
        {
            Br.ReadUInt32();
            return new Fragment31
            {
                References = Br.ReadInt32().Times(ReadRef<Fragment30>).ToArray()
            };
        }

        Fragment36 Read36()
        {
            Br.ReadUInt32();
            var texRef = ReadRef<Fragment31>();
            var aniRef = ReadRef<Fragment2F>();
            Br.ReadUInt32();
            Br.ReadInt32();
            var center = Br.ReadVec3();
            Br.ReadUInt32();
            Br.ReadUInt32();
            Br.ReadUInt32();
            var maxDist = Br.ReadSingle();
            var mins = Br.ReadVec3();
            var maxs = Br.ReadVec3();
            var vertCount = Br.ReadUInt16();
            var tcCount = Br.ReadUInt16();
            var normalCount = Br.ReadUInt16();
            var colorCount = Br.ReadUInt16();
            var polyCount = Br.ReadUInt16();
            var vertPieceCount = Br.ReadUInt16();
            var polyTexCount = Br.ReadUInt16();
            var vertTexCount = Br.ReadUInt16();

            Br.ReadUInt16();
            var scale = (float)(1UL << Br.ReadUInt16());
            var vertices = vertCount.Times(() => new Vector3(Br.ReadInt16(), Br.ReadInt16(), Br.ReadInt16()) / scale + center).ToArray();
            var texcoords = tcCount.Times(() => NewFormat ? Br.ReadVec2() : new Vector2(Br.ReadInt16(), Br.ReadInt16()) / 256).Concat((vertCount - tcCount).Times(() => Vector2.Zero)).ToArray();
            var normals = normalCount.Times(() => new Vector3(Br.ReadSByte(), Br.ReadSByte(), Br.ReadSByte()) / 127).Concat((vertCount - normalCount).Times(() => Vector3.One)).ToArray();
            var colors = colorCount.Times(() => Br.ReadUInt32()).ToArray();
            var polygons = polyCount.Times(() => (Br.ReadUInt16() == 0, (uint)Br.ReadUInt16(), (uint)Br.ReadUInt16(), (uint)Br.ReadUInt16())).ToArray();
            var vertPieces = vertPieceCount.Times(() => ((uint)Br.ReadUInt16(), (uint)Br.ReadUInt16())).ToArray();
            var polyTex = polyTexCount.Times(() => ((uint)Br.ReadUInt16(), (uint)Br.ReadUInt16())).ToArray();

            return new Fragment36
            {
                TextureListReference = texRef,
                Vertices = vertices,
                Normals = normals,
                TexCoords = texcoords,
                Polygons = polygons,
                PolyTexs = polyTex,
                VertBones = vertPieces
            };
        }

        Fragment37 Read37()
        {
            return new Fragment37();
        }

        Reference<T> ReadRef<T>() where T : class => Br.ReadRef<T>(this);

        public int PreResolveRef(int reference)
        {
            if (reference > 0) return reference;
            var name = GetString(-reference);
            return NameIndex.ContainsKey(name) ? NameIndex[name] + 1 : reference;
        }

        public string GetReferenceName(int reference) => reference > 0 ? Fragments[reference - 1].Name : GetString(-reference);
        public object GetReference(int reference) => reference > 0 ? Fragments[reference - 1].Fragment : GetString(-reference);

        string ReadDecodeString(int size) => Encoding.ASCII.GetString(Br.ReadBytes(size).Select((x, i) => (byte)(x ^ StringHashKey[i % 8])).ToArray());

        string GetString(int pos) => StringHash.Substring(pos).Split('\0', 2)[0];
        string GetString(uint pos) => GetString((int)pos);
    }
}
