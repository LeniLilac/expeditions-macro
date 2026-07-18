using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ExpeditionsMacro:settings:v1");

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        byte[] input = Encoding.UTF8.GetBytes(plaintext);
        byte[] output = Transform(input, protect: true);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return string.Empty;
        try
        {
            return Encoding.UTF8.GetString(Transform(Convert.FromBase64String(protectedValue), protect: false));
        }
        catch (FormatException error)
        {
            throw new InvalidDataException("The stored secret is not valid.", error);
        }
    }

    private static byte[] Transform(byte[] input, bool protect)
    {
        GCHandle inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        GCHandle entropyHandle = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
        NativeMethods.DataBlob inputBlob = new() { Length = input.Length, Data = inputHandle.AddrOfPinnedObject() };
        NativeMethods.DataBlob entropyBlob = new() { Length = Entropy.Length, Data = entropyHandle.AddrOfPinnedObject() };
        NativeMethods.DataBlob outputBlob = default;
        try
        {
            bool success = protect
                ? NativeMethods.CryptProtectData(ref inputBlob, "Expeditions Macro", ref entropyBlob, nint.Zero, nint.Zero, NativeMethods.CryptprotectUiForbidden, out outputBlob)
                : NativeMethods.CryptUnprotectData(ref inputBlob, nint.Zero, ref entropyBlob, nint.Zero, nint.Zero, NativeMethods.CryptprotectUiForbidden, out outputBlob);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not protect the saved secret.");
            byte[] output = new byte[outputBlob.Length];
            Marshal.Copy(outputBlob.Data, output, 0, output.Length);
            return output;
        }
        finally
        {
            if (outputBlob.Data != nint.Zero) NativeMethods.LocalFree(outputBlob.Data);
            if (entropyHandle.IsAllocated) entropyHandle.Free();
            if (inputHandle.IsAllocated) inputHandle.Free();
        }
    }
}
