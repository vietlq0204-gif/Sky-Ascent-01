using Save.Abstractions;

//namespace Progress.Save
//{
/// <summary>
/// Adapter: nối GameProgresssManager với SaveSystem qua ISaveable.
/// </summary>
public sealed class ProgressSaveAdapter : ISaveable, IInject<ProgressManager>
{
    private ProgressManager _progress;

    public void Inject(ProgressManager context)
    {
        _progress = context;
    }

    public ProgressSaveAdapter(ProgressManager progress)
    {
        _progress = progress;
    }

    public string Key => "progress";
    public int Version => 1;

    public bool ShouldSave => _progress != null;

    public void BeforeSave()
    {
        _progress.Request_ReturnDataToBase_PrepareSave();
    }

    public object Capture()
    {
        return _progress.CaptureSaveData();

    }

    public void Restore(object data, int version)
    {
        if (data is ProgressSaveData dto)
            _progress.ApplySaveData(dto);
    }

    public void AfterLoad()
    {
        _progress.AfterApplySaveData();
    }


}
//}
