using System.Runtime.InteropServices;
using System.Text;

namespace XNOEdit.Shaders
{
    public partial class ShaderRecompiler : IDisposable
    {
        private const string LibraryName = "XenosRecomp";

        [StructLayout(LayoutKind.Sequential)]
        private struct RecompiledShaderData
        {
            public IntPtr spirv_data;
            public nuint spirv_size;
            public uint spec_constants_mask;
            public IntPtr error_message;
        }

        [LibraryImport(LibraryName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial IntPtr recompile_shader_spirv(
            byte[] shaderData,
            nuint shaderSize,
            byte[] includeData,
            nuint includeSize);

        [LibraryImport(LibraryName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void free_recompiled_shader(IntPtr data);

        private byte[] includeData;

        public ShaderRecompiler(string includeData)
        {
            this.includeData = Encoding.UTF8.GetBytes(includeData);
        }

        public (byte[] spirv, uint specConstantsMask) Recompile(byte[] shaderData)
        {
            var resultPtr = recompile_shader_spirv(
                shaderData,
                (nuint)shaderData.Length,
                includeData,
                (nuint)includeData.Length);

            if (resultPtr == IntPtr.Zero)
            {
                throw new Exception("Shader recompilation failed: null result");
            }

            try
            {
                var result = Marshal.PtrToStructure<RecompiledShaderData>(resultPtr);

                // Check for errors
                if (result.error_message != IntPtr.Zero)
                {
                    string errorMsg = Marshal.PtrToStringAnsi(result.error_message);
                    throw new Exception($"Shader recompilation failed: {errorMsg}");
                }

                // Check if we got valid SPIRV data
                if (result.spirv_data == IntPtr.Zero || result.spirv_size == 0)
                {
                    throw new Exception("Shader recompilation failed: no SPIRV data returned");
                }

                // Copy SPIRV data to managed array
                byte[] spirv = new byte[result.spirv_size];
                Marshal.Copy(result.spirv_data, spirv, 0, (int)result.spirv_size);

                return (spirv, result.spec_constants_mask);
            }
            finally
            {
                free_recompiled_shader(resultPtr);
            }
        }

        public void Dispose()
        {
            // Nothing to dispose currently, but good practice
        }
    }
}
