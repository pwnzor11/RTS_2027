// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game;

/// <summary>
/// U-10 (GAMESYS-08): writes a text file through a temporary file. The temporary file is flushed to disk (<c>Flush(true)</c>) before it is
/// renamed over the target, so a crash or power cut leaves the old file or the new one, never a truncated or empty file.
/// </summary>
public static class AtomicFile
{
    /// <summary>Writes <paramref name="text"/> (UTF-8, no byte order mark) to <paramref name="file"/> atomically.</summary>
    public static void WriteAllText(string file, string text) => Write(file, text, beforeRename: null);

    /// <summary>
    /// The same write; <paramref name="beforeRename"/> is called with the temporary file's path after it has been flushed to disk and
    /// before the rename (tests use it to check the order). Throws the usual I/O exceptions.
    /// </summary>
    public static void Write(string file, string text, Action<string>? beforeRename)
    {
        string tmp = file + ".tmp";
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);   // U-10: the data is on the disk before the rename can make it the file
        }

        beforeRename?.Invoke(tmp);
        File.Move(tmp, file, overwrite: true);
    }
}
