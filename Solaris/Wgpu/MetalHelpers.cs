using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;

namespace XNOEdit.Renderer.Wgpu
{
    internal class NSWindow
    {
        public readonly nint NativePtr;

        public NSWindow(nint ptr)
        {
            NativePtr = ptr;
        }

        public NSView contentView => ObjectiveCRuntime.objc_msgSend<NSView>(NativePtr, "contentView");
    }

    internal struct NSView
    {
        public readonly nint NativePtr;

        public static implicit operator nint(NSView nsView)
        {
            return nsView.NativePtr;
        }

        public NSView(nint ptr)
        {
            NativePtr = ptr;
        }

        public Bool8 wantsLayer
        {
            get => ObjectiveCRuntime.bool8_objc_msgSend(NativePtr, "wantsLayer");
            set => ObjectiveCRuntime.objc_msgSend(NativePtr, "setWantsLayer:", value);
        }

        public nint layer
        {
            get => ObjectiveCRuntime.ptr_objc_msgSend(NativePtr, "layer");
            set => ObjectiveCRuntime.ptr_objc_msgSend(NativePtr, "setLayer:", value);
        }
    }

    internal struct CAMetalLayer
    {
        public readonly nint NativePtr;

        public CAMetalLayer(nint ptr)
        {
            NativePtr = ptr;
        }

        public static CAMetalLayer New()
        {
            return s_class.AllocInit<CAMetalLayer>();
        }

        private static readonly ObjectiveCClass s_class = new(nameof(CAMetalLayer));
    }

    internal unsafe struct ObjectiveCClass
    {
        public readonly nint NativePtr;

        public static implicit operator nint(ObjectiveCClass c)
        {
            return c.NativePtr;
        }

        public ObjectiveCClass(string name)
        {
            var namePtr = SilkMarshal.StringToPtr(name);
            NativePtr = ObjectiveCRuntime.objc_getClass(namePtr);
            SilkMarshal.Free(namePtr);
        }

        public T AllocInit<T>() where T : struct
        {
            var value = ObjectiveCRuntime.ptr_objc_msgSend(NativePtr, "alloc");
            ObjectiveCRuntime.objc_msgSend(value, "init");
            return Unsafe.AsRef<T>(&value);
        }
    }

    internal static unsafe class ObjectiveCRuntime
    {
        private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

        [DllImport(ObjCLibrary)]
        public static extern nint sel_registerName(nint namePtr);

        [DllImport(ObjCLibrary)]
        public static extern byte* sel_getName(nint selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern Bool8 bool8_objc_msgSend(nint receiver, Selector selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern nint ptr_objc_msgSend(nint receiver, Selector selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern nint ptr_objc_msgSend(nint receiver, Selector selector, nint a);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend(nint receiver, Selector selector, byte b);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend(nint receiver, Selector selector);

        [DllImport(ObjCLibrary)]
        public static extern nint objc_getClass(nint namePtr);

        public static T objc_msgSend<T>(nint receiver, Selector selector) where T : struct
        {
            var value = ptr_objc_msgSend(receiver, selector);
            return Unsafe.AsRef<T>(&value);
        }
    }

    internal struct Selector
    {
        public readonly nint NativePtr;

        public Selector(nint ptr)
        {
            NativePtr = ptr;
        }

        public Selector(string name)
        {
            var namePtr = SilkMarshal.StringToPtr(name);
            NativePtr = ObjectiveCRuntime.sel_registerName(namePtr);
            SilkMarshal.Free(namePtr);
        }

        public static implicit operator Selector(string s)
        {
            return new Selector(s);
        }
    }
}
