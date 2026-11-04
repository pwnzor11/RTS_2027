// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.InteropServices;

namespace Rts.Game.Ui.Llm;

/// <summary>Reads text from the system clipboard through SDL2 (already shipped with MonoGame DesktopGL). Returns null on any failure;
/// never throws. Used only by Ctrl+V in the LLM text boxes.</summary>
public static class ClipboardText
{
    [DllImport("SDL2", EntryPoint = "SDL_GetClipboardText", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SdlGetClipboardText();

    [DllImport("SDL2", EntryPoint = "SDL_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SdlFree(IntPtr mem);

    /// <summary>The clipboard text, or null.</summary>
    public static string? Read()
    {
        try
        {
            IntPtr p = SdlGetClipboardText();
            if (p == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUTF8(p);
            }
            finally
            {
                SdlFree(p);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
