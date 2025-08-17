using System;
using System.Formats.Tar;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using LinqToDB.DataProvider.SQLite;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace SqliteEx
{
    internal unsafe partial class MySqliteTokenizer
    {

        [StructLayout(LayoutKind.Sequential)]
        private struct Fts5_api
        {
            public int iVersion;

            // IntPtr pApi,
            // IntPtr zName,
            // IntPtr pUserData,
            // IntPtr pTokenizer,
            // IntPtr xDestroy
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int> xCreateTokenizer;



        }


        [StructLayout(LayoutKind.Sequential, Size = 1024)]
        private struct Fts5_tokenizer
        {

            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int, IntPtr*, int> create;

            public delegate* unmanaged[Stdcall]<IntPtr, void> delete;


            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, IntPtr, int,
                delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int, int, int, int>,
                 int> tokenizer;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CreateTokenize(
            IntPtr ptr,
            IntPtr* argvPtr, // char** 参数数组
            int argc, // 参数数量
            IntPtr* pCtx // 输出的分词器实例句柄 类型是指针的指针
        )
        {
            *pCtx = Marshal.AllocHGlobal(16);
            return raw.SQLITE_OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void DeleteTokenize(
            IntPtr pCtx
        )
        {

            Marshal.FreeHGlobal(pCtx);
        }

        //       int (*xTokenize)(
        //       Fts5Tokenizer*,
        //       void *pCtx,
        //       int flags,            /* Mask of FTS5_TOKENIZE_* flags */
        //       const char *pText, 
        //       int nText,
        //       int (*xToken)(
        //         void *pCtx,         /* Copy of 2nd argument to xTokenize() */
        //         int tflags,         /* Mask of FTS5_TOKEN_* flags */
        //         const char *pToken, /* Pointer to buffer containing token */
        //         int nToken,         /* Size of token in bytes */
        //         int iStart,         /* Byte offset of token within input text */
        //         int iEnd            /* Byte offset of end of token within input text */
        //       )
        //   );



        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int TokenizeTokenize(
            IntPtr obj,
            IntPtr pCtx,
            int flags, // FTS5_TOKENIZE_* 标志
            IntPtr pText, // 输入文本
            int nText,
            delegate* unmanaged[Stdcall]<
                IntPtr,
                int,
                IntPtr,
                int,
                int,
                int,
                int> callback // 回调函数指针
        )
        {

            ReadOnlySpan<byte> text = new((void*)pText, nText);
            Span<byte> buf = stackalloc byte[4];

            while (text.Length != 0)
            {
                int offset = nText - text.Length;
                if (Rune.DecodeFromUtf8(text, out var item, out int count) != System.Buffers.OperationStatus.Done)
                {
                    return raw.SQLITE_ERROR;
                }


                //Console.WriteLine($"text.length{text.Length}: count{count}");

                _ = text[..count];

                text = text[count..];

                
                if (Rune.IsControl(item) || 
                    Rune.IsWhiteSpace(item) ||
                    Rune.IsPunctuation(item) ||
                    Rune.IsSeparator(item)||
                    Rune.IsSymbol(item)){
                        continue;
                }


                {
                    IntPtr pToken = pText + offset;

                    int nToken = count;

                    
                    int iStart = offset;

                    int iEnd = iStart + count;


                    int res = callback(pCtx, 0, pToken, nToken, iStart, iEnd);


                    if (res != raw.SQLITE_OK)
                    {
                        return res;
                    }


                    
                }

                if(item.IsAscii && Rune.IsUpper(item)){
                    var length = Rune.ToLowerInvariant(item).EncodeToUtf8(buf);

                  
                    fixed(byte* p = &buf[0])
                    {

                        IntPtr pToken = new(p);

                        int nToken = length;


                        //后面这两个参数应该指的是输入字符串中的偏移
                
                        int iStart = offset;

                        int iEnd = iStart + count;

                        //添加同义词标志FTS5_TOKEN_COLOCATED 
                        int res = callback(pCtx,0x0001, pToken, nToken, iStart, iEnd);


                        if (res != raw.SQLITE_OK)
                        {
                            return res;
                        }




                    }

                    

                }

            }


            return raw.SQLITE_OK;

        }


        [LibraryImport("e_sqlite3", EntryPoint = "sqlite3_bind_pointer", StringMarshalling = StringMarshalling.Utf8)]
        private static partial int sqlite3_bind_pointer(sqlite3_stmt stmt, int index, IntPtr p, IntPtr typeName, IntPtr func);

        static void ThrowException(string msg, int errorCode, sqlite3? handle = null)
        {
            string s;
            if (handle is null)
            {
                s = msg;
            }
            else{
                s = $"{msg}:{raw.sqlite3_errmsg(handle).utf8_to_string()}";
            }
            
            throw new SqliteException(s, errorCode);
        }

        public static void RegisterCustomTokenizer(SqliteConnection conn, string tokenizerName)
        {

            // 获取 FTS5 API 结构体指针
            sqlite3_stmt stmt;

            {

                int res = raw.sqlite3_prepare_v3(conn.Handle, "SELECT fts5(?1);", 0, out stmt);

                if (res != raw.SQLITE_OK)
                {
                    ThrowException("sqlite3_prepare_v3 error", res, conn.Handle);
                }
            }


            IntPtr fts5_api_p = 0;

            unsafe
            {

                var typeName = "fts5_api_ptr\0"u8;

                //理论上应该不需要固定
                fixed (byte* p = &typeName[0])
                {
                    //根据sqlite文档,类型字符串必须是静态字符串也就是说在sqlite3_bind_pointer返回后
                    //或者至少在查询完成之前应该需要保持有效, 内部不会保留副本
                    //所以当P/Invoke自动转换UTF8时会因为类型字符串已经失效而无法获取指针

                    int res = sqlite3_bind_pointer(stmt,
                    1,
                    new IntPtr(&fts5_api_p),
                    new IntPtr(p),
                    IntPtr.Zero);

                    if (res != raw.SQLITE_OK)
                    {

                        ThrowException("sqlite3_bind_pointer error", res, conn.Handle);
                    }
                }



            }



            {

                int res = raw.sqlite3_step(stmt);

                if (res != raw.SQLITE_ROW)
                {
                    ThrowException("sqlite3_step error",res, conn.Handle);
                }

                if (fts5_api_p == 0)
                {
                    ThrowException("fts5_api_p is 0 error", raw.SQLITE_ERROR, conn.Handle);
                }

            }


            _ = raw.sqlite3_finalize(stmt);

            ref Fts5_api v = ref Unsafe.AsRef<Fts5_api>((void*)fts5_api_p);

            if(v.iVersion < 2){
                ThrowException("Fts5_api.iVersion < 2 error", raw.SQLITE_ERROR, conn.Handle);
            }

            Fts5_tokenizer token = new()
            {
                create = &CreateTokenize,
                delete = &DeleteTokenize,

                tokenizer = &TokenizeTokenize
            };

            unsafe
            {

                var nameBytes = Encoding.UTF8.GetBytes(tokenizerName + "\0");


                fixed (byte* name_p = &nameBytes[0])
                {
                    int res = v.xCreateTokenizer(fts5_api_p,
                    new IntPtr(name_p),
                    0,
                    new IntPtr(&token),
                    0);

                    if (res != raw.SQLITE_OK)
                    {
                        ThrowException("xCreateTokenizer error", res, conn.Handle);
                    }
                }



            }

        }


    }

}