using Marathon.Formats.Archive;
using Marathon.IO.Types.FileSystem;
using XNOEdit.ModelResolver;

namespace XNOEdit.Services
{
    public enum LoadStepType
    {
        Object,
        Mission,
        Stage
    }

    public abstract class LoadStep
    {
        public abstract LoadStepType Type { get; }
        public abstract Task ExecuteAsync(
            FileLoaderService loader,
            ArcFile? shaderArchive,
            IProgress<LoadProgress> progress,
            CancellationToken token);
    }

    public class ObjectLoadStep : LoadStep
    {
        public override LoadStepType Type => LoadStepType.Object;
        public IFile File { get; }
        public ObjectLoadResult? Result { get; private set; }

        public ObjectLoadStep(IFile file)
        {
            File = file;
        }

        public override async Task ExecuteAsync(
            FileLoaderService loader,
            ArcFile? shaderArchive,
            IProgress<LoadProgress> progress,
            CancellationToken token)
        {
            Result = await loader.ReadXnoAsync(File, shaderArchive, progress, token);
        }
    }

    public class StageLoadStep : LoadStep
    {
        public override LoadStepType Type => LoadStepType.Stage;
        public ArcFile ArcFile { get; }
        public StageLoadResult? Result { get; private set; }

        public StageLoadStep(ArcFile arcFile)
        {
            ArcFile = arcFile;
        }

        public override async Task ExecuteAsync(
            FileLoaderService loader,
            ArcFile? shaderArchive,
            IProgress<LoadProgress> progress,
            CancellationToken token)
        {
            Result = await loader.ReadArcAsync(ArcFile, shaderArchive, progress, token);
        }
    }

    public class MissionLoadStep : LoadStep
    {
        public override LoadStepType Type => LoadStepType.Mission;
        public IFile File { get; }
        public ResolverContext ResolverContext { get; }
        public MissionLoadResult? Result { get; private set; }

        public MissionLoadStep(IFile file, ResolverContext resolverContext)
        {
            File = file;
            ResolverContext = resolverContext;
        }

        public override async Task ExecuteAsync(
            FileLoaderService loader,
            ArcFile? shaderArchive,
            IProgress<LoadProgress> progress,
            CancellationToken token)
        {
            Result = await loader.ReadMissionAsync(File, ResolverContext, shaderArchive, progress, token);
        }
    }

    public class LoadChain
    {
        private readonly List<LoadStep> _steps = [];
        private readonly FileLoaderService _loader;
        private readonly ArcFile? _shaderArchive;

        private CancellationTokenSource? _cts;
        private Task? _runningTask;
        private int _currentStepIndex;

        public event Action<LoadStep>? StepCompleted;
        public event Action? ChainCompleted;
        public event Action<Exception>? ChainFailed;
        public event Action<LoadProgress>? ProgressChanged;

        public bool IsRunning => _runningTask is { IsCompleted: false };

        public LoadChain(FileLoaderService loader, ArcFile? shaderArchive)
        {
            _loader = loader;
            _shaderArchive = shaderArchive;
        }

        public LoadChain Add(LoadStep step)
        {
            if (IsRunning)
                throw new InvalidOperationException("Cannot add steps while chain is running");

            _steps.Add(step);
            return this;
        }

        public LoadChain AddXno(IFile file) => Add(new ObjectLoadStep(file));
        public LoadChain AddSet(IFile file, ResolverContext resolverContext) => Add(new MissionLoadStep(file, resolverContext));
        public LoadChain AddArc(ArcFile arcFile) => Add(new StageLoadStep(arcFile));

        public void Start()
        {
            if (IsRunning)
                Cancel();

            _cts = new CancellationTokenSource();
            _currentStepIndex = 0;
            _runningTask = RunAsync(_cts.Token);
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _runningTask = null;
            _currentStepIndex = 0;
        }

        public void Clear()
        {
            Cancel();
            _steps.Clear();
        }

        private async Task RunAsync(CancellationToken token)
        {
            var progress = new Progress<LoadProgress>(p => ProgressChanged?.Invoke(p));

            try
            {
                for (_currentStepIndex = 0; _currentStepIndex < _steps.Count; _currentStepIndex++)
                {
                    token.ThrowIfCancellationRequested();

                    var step = _steps[_currentStepIndex];
                    await step.ExecuteAsync(_loader, _shaderArchive, progress, token);

                    StepCompleted?.Invoke(step);
                }

                ChainCompleted?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Cancelled, don't report as failure
            }
            catch (Exception ex)
            {
                ChainFailed?.Invoke(ex);
            }
        }
    }
}
