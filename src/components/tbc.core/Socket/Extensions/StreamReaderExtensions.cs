using System.IO;
using System.Threading.Tasks;

namespace Tbc.Core.Socket.Extensions;

public static class StreamReaderExtensions
{
    public static async Task<byte[]> ReadExactly(this Stream stream, int length)
    {
        var buffer = new byte[length];
        var bytesRead = 0;

        while (bytesRead < length)
            bytesRead += await stream.ReadAsync(buffer, bytesRead, length - bytesRead);

        return buffer;
    }
}