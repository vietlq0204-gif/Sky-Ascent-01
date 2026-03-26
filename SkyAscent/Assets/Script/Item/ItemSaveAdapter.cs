using ViT.SaveKit.Abstractions;

public class ItemSaveAdapter : ISaveable, IInject<ItemManager>
{
    ItemManager _itemManager;

    public ItemSaveAdapter(ItemManager itemManager)
    {
        _itemManager = itemManager;
    }

    public void Inject(ItemManager context)
    {
        _itemManager = context;
    }

    public string Key => "item";
    public int Version => 1;
    public bool ShouldSave => _itemManager != null;

    public void BeforeSave()
    {
        // Prepare data before save if needed.
    }

    public object Capture()
    {
        return _itemManager.CaptureState();
    }

    public void Restore(object data, int version)
    {
        if (data is ItemSaveData dto)
        {
            _itemManager.ApplySaveData(dto);
        }
    }

    public void AfterLoad()
    {
        _itemManager.AfterApplySaveData();
    }
}
