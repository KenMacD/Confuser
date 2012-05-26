﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;
using Mono.Cecil.Metadata;
using System.IO;

namespace Confuser.Core.Confusions
{
    partial class AntiTamperConfusion
    {
        class Mem : IAntiTamper
        {
            TypeDefinition root;
            int key0;
            long key1;
            int key2;
            int key3;
            int key4;
            byte key5;
            List<int> excludes;
            uint[] rvas;
            uint[] ptrs;
            byte[][] codes;
            uint codeLen;
            uint sectRaw;
            string sectName;

            public Action<IMemberDefinition, HelperAttribute> AddHelper { get; set; }

            public void InitPhase1(ModuleDefinition mod)
            {
                Random rand = new Random();
                byte[] dat = new byte[25];
                rand.NextBytes(dat);
                key0 = BitConverter.ToInt32(dat, 0);
                key1 = BitConverter.ToInt64(dat, 4);
                key2 = BitConverter.ToInt32(dat, 12);
                key3 = BitConverter.ToInt32(dat, 16);
                key4 = BitConverter.ToInt32(dat, 20);
                key5 = dat[24];
                sectName = Convert.ToBase64String(MD5.Create().ComputeHash(dat)).Substring(0, 8);
            }

            public void Phase1(ModuleDefinition mod)
            {
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                root = CecilHelper.Inject(mod, i.MainModule.GetType("AntiTamperMem"));
                mod.Types.Add(root);
                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                cctor.Body.GetILProcessor().InsertBefore(0, Instruction.Create(OpCodes.Call, root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize")));

                MethodDefinition init = root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize");
                foreach (Instruction inst in init.Body.Instructions)
                {
                    if (inst.Operand is int)
                    {
                        switch ((int)inst.Operand)
                        {
                            case 0x11111111:
                                inst.Operand = key0; break;
                            case 0x33333333:
                                inst.Operand = key2; break;
                            case 0x44444444:
                                inst.Operand = key3; break;
                            case 0x55555555:
                                inst.Operand = key4; break;
                            case 0x66666666:
                                inst.Operand = (int)sectName.ToCharArray().Sum(_ => (int)_); break;
                        }
                    }
                    else if (inst.Operand is long && (long)inst.Operand == 0x2222222222222222)
                        inst.Operand = key1;
                }
                MethodDefinition dec = root.Methods.FirstOrDefault(mtd => mtd.Name == "Decrypt");
                foreach (Instruction inst in dec.Body.Instructions)
                    if (inst.Operand is int && (int)inst.Operand == 0x11111111)
                        inst.Operand = (int)key5;

                root.Name = ObfuscationHelper.GetNewName("AntiTamperModule" + Guid.NewGuid().ToString());
                root.Namespace = "";
                AddHelper(root, HelperAttribute.NoInjection);
                foreach (MethodDefinition mtdDef in root.Methods)
                {
                    mtdDef.Name = ObfuscationHelper.GetNewName(mtdDef.Name + Guid.NewGuid().ToString());
                    AddHelper(mtdDef, HelperAttribute.NoInjection);
                }
            }

            public void InitPhase2(ModuleDefinition mod)
            {
                //
            }

            public void Phase2(IProgresser progresser, ModuleDefinition mod)
            {
                Queue<TypeDefinition> q = new Queue<TypeDefinition>();
                excludes = new List<int>();
                excludes.Add((int)mod.GetType("<Module>").GetStaticConstructor().MetadataToken.RID - 1);
                q.Enqueue(root);
                while (q.Count != 0)
                {
                    TypeDefinition typeDef = q.Dequeue();
                    foreach (MethodDefinition mtd in typeDef.Methods)
                        excludes.Add((int)mtd.MetadataToken.RID - 1);
                    foreach (TypeDefinition t in typeDef.NestedTypes)
                        q.Enqueue(t);
                }
            }

            public void Phase3(MetadataProcessor.MetadataAccessor accessor)
            {
                MethodTable tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);
                rvas = new uint[tbl.Length];
                ptrs = new uint[tbl.Length];
                codes = new byte[tbl.Length][];
                for (int i = 0; i < tbl.Length; i++)
                {
                    if (excludes.Contains(i) || (tbl[i].Col2 & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL) continue;
                    rvas[i] = tbl[i].Col1;
                }
                codeLen = (uint)accessor.Codes.Length;
            }

            public void Phase4(MetadataProcessor.ImageAccessor accessor)
            {
                uint size = 2 + 4 + codeLen;
                for (int i = 0; i < codes.Length; i++)
                {
                    size += 4;
                    if (rvas[i] == 0) continue;
                    size += 8;
                }
                size = (((uint)size + 0x7f) & ~0x7fu) + 0x28;
                Section prev = accessor.Sections[accessor.Sections.Count - 1];
                accessor.Sections[0].Characteristics = 0x60000020;
                Section sect = accessor.CreateSection(sectName, size, 0x40000040, prev);
                accessor.Sections.Add(sect);
            }


            void ExtractCodes(Stream stream, MetadataProcessor.ImageAccessor accessor, out uint csOffset, out uint sn, out uint snLen)
            {
                Random rand = new Random();
                BinaryReader rdr = new BinaryReader(stream);
                stream.Seek(0x3c, SeekOrigin.Begin);
                uint offset = rdr.ReadUInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Seek(0x6, SeekOrigin.Current);
                uint sections = rdr.ReadUInt16();
                stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
                bool pe32 = (rdr.ReadUInt16() == 0x010b);
                csOffset = offset + 0x40;
                stream.Seek(offset = offset + (pe32 ? 0xE0U : 0xF0U), SeekOrigin.Begin);   //sections

                for (int i = 0; i < rvas.Length; i++)
                {
                    if (rvas[i] == 0) continue;
                    ptrs[i] = accessor.ResolveVirtualAddress(rvas[i]);
                    stream.Seek(ptrs[i], SeekOrigin.Begin);
                    byte b = rdr.ReadByte();
                    if ((b & 0x3) == 0x2)
                    {
                        stream.Seek((uint)b >> 2, SeekOrigin.Current);
                    }
                    else
                    {
                        stream.Seek(-1, SeekOrigin.Current);
                        ushort f = rdr.ReadUInt16();
                        stream.Seek(2, SeekOrigin.Current);
                        uint size = rdr.ReadUInt32();
                        stream.Seek(4 + size, SeekOrigin.Current);
                        if ((f & 0x80) != 0)
                        {
                            stream.Seek((stream.Position + 3) & ~3, SeekOrigin.Begin);
                            bool more;
                            do
                            {
                                byte fl = rdr.ReadByte();
                                more = ((fl & 0x80) != 0);
                                if ((fl & 0x40) != 0)
                                {
                                    stream.Seek(-1, SeekOrigin.Current);
                                    uint sectLen = rdr.ReadUInt32() >> 8;
                                    stream.Seek(-4 + sectLen, SeekOrigin.Current);
                                }
                                else
                                {
                                    byte sectLen = rdr.ReadByte();
                                    stream.Seek(-1 + sectLen, SeekOrigin.Current);
                                }
                            } while (more);
                        }
                    }
                    long len = stream.Position - ptrs[i];
                    stream.Seek(ptrs[i], SeekOrigin.Begin);
                    codes[i] = rdr.ReadBytes((int)len);
                    stream.Seek(ptrs[i], SeekOrigin.Begin);
                    byte[] bs = new byte[len];
                    //rand.NextBytes(bs);
                    stream.Write(bs, 0, (int)len);
                }

                stream.Seek(offset - 16, SeekOrigin.Begin);
                uint mdDir = accessor.ResolveVirtualAddress(rdr.ReadUInt32());
                stream.Seek(mdDir + 0x20, SeekOrigin.Begin);
                sn = accessor.ResolveVirtualAddress(rdr.ReadUInt32());
                snLen = rdr.ReadUInt32();
            }
            static byte[] Encrypt(byte[] buff, byte[] dat, out byte[] iv, byte key)
            {
                dat = (byte[])dat.Clone();
                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(buff);
                for (int i = 0; i < dat.Length; i += 64)
                {
                    byte[] o = new byte[64];
                    int len = dat.Length <= i + 64 ? dat.Length : i + 64;
                    Buffer.BlockCopy(dat, i, o, 0, len - i);
                    for (int j = i; j < len; j++)
                        dat[j] ^= (byte)(c[j - i] ^ key);
                    c = sha.ComputeHash(o);
                }

                Rijndael ri = Rijndael.Create();
                ri.GenerateIV(); iv = ri.IV;
                MemoryStream ret = new MemoryStream();
                using (CryptoStream cStr = new CryptoStream(ret, ri.CreateEncryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Write))
                    cStr.Write(dat, 0, dat.Length);
                return ret.ToArray();
            }
            static byte[] Decrypt(byte[] buff, byte[] iv, byte[] dat, byte key)
            {
                Rijndael ri = Rijndael.Create();
                byte[] ret = new byte[dat.Length];
                MemoryStream ms = new MemoryStream(dat);
                using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Read))
                { cStr.Read(ret, 0, dat.Length); }

                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(buff);
                for (int i = 0; i < ret.Length; i += 64)
                {
                    int len = ret.Length <= i + 64 ? ret.Length : i + 64;
                    for (int j = i; j < len; j++)
                        ret[j] ^= (byte)(c[j - i] ^ key);
                    c = sha.ComputeHash(ret, i, len - i);
                }
                return ret;
            }

            public void Phase5(Stream stream, MetadataProcessor.ImageAccessor accessor)
            {
                stream.Seek(0, SeekOrigin.Begin);
                uint csOffset;
                uint sn;
                uint snLen;
                ExtractCodes(stream, accessor, out csOffset, out sn, out snLen);
                stream.Position = 0;

                MemoryStream ms = new MemoryStream();
                ms.WriteByte(0xd6);
                ms.WriteByte(0x6f);
                BinaryWriter wtr = new BinaryWriter(ms);
                wtr.Write((uint)codes.Length);
                for (int i = 0; i < codes.Length; i++)
                {
                    wtr.Write((int)(ptrs[i] ^ key4));
                    if (ptrs[i] == 0) continue;
                    wtr.Write((int)(rvas[i] ^ key4));
                    wtr.Write(codes[i].Length);
                    wtr.Write(codes[i]);
                }

                byte[] buff;
                BinaryReader sReader = new BinaryReader(stream);
                using (MemoryStream str = new MemoryStream())
                {
                    var mdPtr = accessor.ResolveVirtualAddress(accessor.GetTextSegmentRange(TextSegment.MetadataHeader).Start);
                    stream.Position = mdPtr + 12;
                    stream.Position += sReader.ReadUInt32() + 4;
                    stream.Position += 2;

                    ushort streams = sReader.ReadUInt16();

                    for (int i = 0; i < streams; i++)
                    {
                        uint offset = mdPtr + sReader.ReadUInt32();
                        uint size = sReader.ReadUInt32();

                        int c = 0;
                        while (sReader.ReadByte() != 0) c++;
                        long ori = stream.Position += (((c + 1) + 3) & ~3) - (c + 1);

                        stream.Position = offset;
                        str.Write(sReader.ReadBytes((int)size), 0, (int)size);
                        stream.Position = ori;
                    }

                    buff = str.ToArray();
                }

                byte[] iv;
                byte[] dat = Encrypt(buff, ms.ToArray(), out iv, key5);

                byte[] md5 = MD5.Create().ComputeHash(buff);
                long checkSum = BitConverter.ToInt64(md5, 0) ^ BitConverter.ToInt64(md5, 8);
                wtr = new BinaryWriter(stream);
                stream.Seek(csOffset, SeekOrigin.Begin);
                wtr.Write(accessor.GetTextSegmentRange(TextSegment.MetadataHeader).Start ^ (uint)key0);
                stream.Seek(accessor.GetSection(sectName).PointerToRawData, SeekOrigin.Begin);
                wtr.Write(checkSum ^ key1);
                wtr.Write(sn);
                wtr.Write(snLen);
                wtr.Write(iv.Length ^ key2);
                wtr.Write(iv);
                wtr.Write(dat.Length ^ key3);
                wtr.Write(dat);
            }
        }
    }
}
