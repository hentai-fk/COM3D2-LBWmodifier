using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using static CRCEdit.ConfigBridgeData;

namespace LBWmodifier
{
    internal class FileTool
    {
        public static AFileSystemBase[] getFileSystemArray()
        {
            return new AFileSystemBase[]
            {
                GameUty.FileSystemMod,
                GameMain.Instance?.ScriptMgr?.file_system,
                GameUty.FileSystem,
                GameUty.FileSystemOld,
                Traverse.Create(typeof(GameUty)).Field("m_KcesExportFileSystem").GetValue() as AFileSystemBase,
                Traverse.Create(typeof(GameUty)).Field("m_CrcFileSystem").GetValue() as AFileSystemBase,
            };
        }

        public static CompatibleAFile OpenFileCompatible(string filename)
        {
            foreach (var afileSystemBase in getFileSystemArray())
            {
                if (afileSystemBase != null)
                {
                    var fd = afileSystemBase.FileOpen(filename);
                    if (fd.IsValid())
                    {
                        return new CompatibleAFile(afileSystemBase, fd);
                    }
                    fd.Dispose();
                }
            }
            return null;
        }

        private static string getBytesMD5(byte[] bytes)
        {
            var bs = MD5.Create().ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var item in bs)
            {
                sb.Append(item.ToString("x2"));
            }
            return sb.ToString();
        }

        public static string getTextMD5(string text)
        {
            return getBytesMD5(Encoding.UTF8.GetBytes(text));
        }

        public static string getFileMD5(string file)
        {
            return getBytesMD5(File.ReadAllBytes(file));
        }
    }

    internal class CompatibleAFile : IDisposable
    {
        public AFileSystemBase system;
        public AFileBase file;

        public CompatibleAFile(AFileSystemBase system, AFileBase file)
        {
            this.system = system;
            this.file = file;
        }

        public void Dispose()
        {
            this.file.Dispose();
            this.file = null;
        }
    }
}
