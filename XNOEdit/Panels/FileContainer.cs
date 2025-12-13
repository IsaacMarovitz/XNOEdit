using System.Collections.ObjectModel;
using Marathon.Formats.Archive;
using Marathon.IO.Types.FileSystem;

namespace XNOEdit.Panels
{
    public class FileContainer
    {
        private readonly string _name;
        private readonly List<IFile> _files;

        public FileContainer(string name)
        {
            _name = name;
            _files = [];
        }

        public void Clear()
        {
            _files.Clear();
        }

        public void AddFromArcPath(string arcPath, string pattern)
        {
            var archive = new ArcFile(arcPath);
            _files.AddRange(archive.EnumerateFiles(pattern, SearchOption.AllDirectories));
        }

        public void Add(IFile file)
        {
            _files.Add(file);
        }

        public void Render(string searchText, Action<ImGuiComponents.File, ReadOnlyCollection<IFile>> triggerFileLoad)
        {
            var files = _files.Select(x => new ImGuiComponents.File(x.Name, x.Name));
            ImGuiComponents.RenderFilesList(_name, files, x =>
            {
                triggerFileLoad(x, _files.AsReadOnly());
            }, searchText);
        }
    }
}
