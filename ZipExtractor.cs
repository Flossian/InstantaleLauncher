using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace InstantaleLauncher
{
    /// <summary>
    /// 追加参照なしで ZIP を展開する最小実装(System.dll の DeflateStream のみ使用)。
    /// 圧縮方式は 0=無圧縮 と 8=Deflate のみ対応(GitHub のリリース zip はこの範囲)。
    /// 全エントリが単一のトップレベルフォルダで包まれている場合は、そのフォルダを剥がして展開する。
    /// ZIP64・暗号化・分割書庫には非対応。
    /// </summary>
    public static class ZipExtractor
    {
        private sealed class Entry
        {
            public string Name;
            public ushort Method;
            public long CompressedSize;
            public long LocalHeaderOffset;
        }

        /// <summary>zipPath を destDir 直下へ展開する。</summary>
        public static void ExtractToDirectory(string zipPath, string destDir)
        {
            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var entries = ReadCentralDirectory(fs);
                string prefix = CommonRootPrefix(entries);

                foreach (var e in entries)
                {
                    string relName = e.Name;
                    if (prefix.Length > 0 && relName.StartsWith(prefix, StringComparison.Ordinal))
                        relName = relName.Substring(prefix.Length);
                    if (relName.Length == 0) continue;

                    if (relName.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(SafeCombine(destDir, relName));
                        continue;
                    }

                    string target = SafeCombine(destDir, relName);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    ExtractEntry(fs, e, target);
                }
            }
        }

        /// <summary>中央ディレクトリを読んで全エントリのメタ情報を得る。</summary>
        private static List<Entry> ReadCentralDirectory(FileStream fs)
        {
            long fileLen = fs.Length;
            int maxTail = (int)Math.Min(fileLen, 65557); // EOCD 最大長(22 + 65535 コメント)
            fs.Seek(fileLen - maxTail, SeekOrigin.Begin);
            byte[] tail = new byte[maxTail];
            ReadFull(fs, tail, 0, maxTail);

            int eocd = -1;
            for (int i = maxTail - 22; i >= 0; i--)
            {
                if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                {
                    eocd = i;
                    break;
                }
            }
            if (eocd < 0) throw new InvalidDataException("End-of-central-directory record not found (not a zip?)");

            int total = BitConverter.ToUInt16(tail, eocd + 10);
            uint cdOffset = BitConverter.ToUInt32(tail, eocd + 16);

            var entries = new List<Entry>(total);
            fs.Seek(cdOffset, SeekOrigin.Begin);
            var br = new BinaryReader(fs, Encoding.UTF8, true);
            for (int i = 0; i < total; i++)
            {
                uint sig = br.ReadUInt32();
                if (sig != 0x02014b50) break; // 中央ディレクトリヘッダ以外に達したら終了
                br.ReadUInt16();                       // version made by
                br.ReadUInt16();                       // version needed
                ushort flags = br.ReadUInt16();
                ushort method = br.ReadUInt16();
                br.ReadUInt16();                       // mod time
                br.ReadUInt16();                       // mod date
                br.ReadUInt32();                       // crc32
                uint compSize = br.ReadUInt32();
                br.ReadUInt32();                       // uncompressed size
                ushort nameLen = br.ReadUInt16();
                ushort extraLen = br.ReadUInt16();
                ushort commentLen = br.ReadUInt16();
                br.ReadUInt16();                       // disk number start
                br.ReadUInt16();                       // internal attrs
                br.ReadUInt32();                       // external attrs
                uint localOffset = br.ReadUInt32();
                byte[] nameBytes = br.ReadBytes(nameLen);
                if (extraLen > 0) br.ReadBytes(extraLen);
                if (commentLen > 0) br.ReadBytes(commentLen);

                entries.Add(new Entry
                {
                    Name = DecodeName(nameBytes, flags).Replace('\\', '/'),
                    Method = method,
                    CompressedSize = compSize,
                    LocalHeaderOffset = localOffset,
                });
            }
            return entries;
        }

        /// <summary>1エントリのデータをローカルヘッダから読み出して target へ書き出す。</summary>
        private static void ExtractEntry(FileStream fs, Entry e, string target)
        {
            fs.Seek(e.LocalHeaderOffset, SeekOrigin.Begin);
            var br = new BinaryReader(fs, Encoding.UTF8, true);
            uint sig = br.ReadUInt32();
            if (sig != 0x04034b50) throw new InvalidDataException("Bad local file header for " + e.Name);
            br.ReadUInt16();                 // version needed
            br.ReadUInt16();                 // flags
            br.ReadUInt16();                 // method(中央ディレクトリ側を採用)
            br.ReadUInt16();                 // mod time
            br.ReadUInt16();                 // mod date
            br.ReadUInt32();                 // crc32
            br.ReadUInt32();                 // comp size(ローカルは 0 のことがある)
            br.ReadUInt32();                 // uncomp size
            ushort nameLen = br.ReadUInt16();
            ushort extraLen = br.ReadUInt16();
            long dataOffset = e.LocalHeaderOffset + 30 + nameLen + extraLen;

            fs.Seek(dataOffset, SeekOrigin.Begin);
            byte[] comp = new byte[checked((int)e.CompressedSize)];
            ReadFull(fs, comp, 0, comp.Length);

            using (var outFile = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (e.Method == 0) // 無圧縮
                {
                    outFile.Write(comp, 0, comp.Length);
                }
                else if (e.Method == 8) // Deflate
                {
                    using (var ms = new MemoryStream(comp))
                    using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                    {
                        byte[] buf = new byte[81920];
                        int n;
                        while ((n = ds.Read(buf, 0, buf.Length)) > 0)
                            outFile.Write(buf, 0, n);
                    }
                }
                else
                {
                    throw new NotSupportedException("Unsupported zip compression method " + e.Method + " for " + e.Name);
                }
            }
        }

        /// <summary>全ファイルエントリが同一のトップレベルフォルダ配下なら、その "フォルダ名/" を返す。無ければ空文字。</summary>
        private static string CommonRootPrefix(List<Entry> entries)
        {
            string root = null;
            foreach (var e in entries)
            {
                int slash = e.Name.IndexOf('/');
                if (slash <= 0) return "";     // トップレベルにファイル/フォルダが直置き → 剥がさない
                string seg = e.Name.Substring(0, slash);
                if (root == null) root = seg;
                else if (!string.Equals(root, seg, StringComparison.Ordinal)) return "";
            }
            return root == null ? "" : root + "/";
        }

        /// <summary>ファイル名フラグ(bit11=UTF8)に従って名前を復号する。</summary>
        private static string DecodeName(byte[] bytes, ushort flags)
        {
            if ((flags & 0x0800) != 0) return Encoding.UTF8.GetString(bytes);
            try { return Encoding.GetEncoding(437).GetString(bytes); }
            catch { return Encoding.UTF8.GetString(bytes); }
        }

        /// <summary>展開先が destDir の外へ出る不正エントリ(zip slip)を弾いて絶対パスを返す。</summary>
        private static string SafeCombine(string destDir, string relName)
        {
            string rel = relName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(destDir, rel));
            string rootFull = Path.GetFullPath(destDir);
            string rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Unsafe zip entry path: " + relName);
            return full;
        }

        /// <summary>count バイトを確実に読み切る。</summary>
        private static void ReadFull(Stream s, byte[] buf, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buf, offset + total, count - total);
                if (n <= 0) throw new EndOfStreamException();
                total += n;
            }
        }
    }
}
