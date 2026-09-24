using System.Collections.ObjectModel;
using Marathon.Formats.Archive;
using Marathon.IO.Types.FileSystem;

namespace XNOEdit.Panels
{
    public record struct FileEntry(IFile File, ArcFile ArcFile);

    public class FileContainer
    {
        public string Name { get; }
        private readonly List<FileEntry> _files;

        public FileContainer(string name)
        {
            Name = name;
            _files = [];
        }

        public void Clear()
        {
            _files.Clear();
        }

        public void AddFromArc(ArcFile archive, string pattern)
        {
            _files.AddRange(archive.EnumerateFiles(pattern, SearchOption.AllDirectories)
                .Select(x => new FileEntry(x, archive)));
        }

        public void Add(IFile file, ArcFile archive)
        {
            _files.Add(new FileEntry(file, archive));
        }

        public void RenderTabItem(string searchText, Action<ImGuiComponents.File, ReadOnlyCollection<FileEntry>> triggerFileLoad)
        {
            var files = _files.Select(x => new ImGuiComponents.File(x.File.Name, x.File.Name));
            ImGuiComponents.RenderFilesListTabItem(Name, files, x =>
            {
                triggerFileLoad(x, _files.AsReadOnly());
            }, searchText);
        }

        public void Render(string searchText, Action<ImGuiComponents.File, ReadOnlyCollection<FileEntry>> triggerFileLoad)
        {
            var files = _files.Select(x => new ImGuiComponents.File(x.File.Name, x.File.Name));
            ImGuiComponents.RenderFilesList(files, x =>
            {
                triggerFileLoad(x, _files.AsReadOnly());
            }, searchText);
        }
    }
}
