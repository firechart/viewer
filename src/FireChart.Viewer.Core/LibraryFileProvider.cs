using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace FireChart.Viewer.Core;

// Lets {% render "partials/row" %} find partials in the library: the key is the file name without folders and extension
internal sealed class LibraryFileProvider(TemplateLibrary library) : IFileProvider
{
    public IFileInfo GetFileInfo(string subpath)
    {
        var key = Path.GetFileNameWithoutExtension(subpath);
        var text = library.Find(key);
        return text is null ? new NotFoundFileInfo(subpath) : new TextFileInfo(key, text);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return NotFoundDirectoryContents.Singleton;
    }

    public IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }

    private sealed class TextFileInfo(string name, string text) : IFileInfo
    {
        public bool Exists => true;
        public long Length => Encoding.UTF8.GetByteCount(text);
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(text));
        }
    }
}
