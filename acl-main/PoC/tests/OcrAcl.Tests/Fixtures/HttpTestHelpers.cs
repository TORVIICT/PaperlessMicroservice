using System.Net.Http.Headers;
using System.Text;

namespace OcrAcl.Tests.Fixtures;

public static class HttpTestHelpers
{
    public static StringContent JsonContent(string body) =>
        new(body, Encoding.UTF8, "application/json");

    public static StringContent ContentWithMediaType(string body, string mediaType) =>
        new(body, Encoding.UTF8, mediaType);

    public static StringContent JsonContentWithoutContentType(string body)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = null;
        return content;
    }
}
