using Unity.VisualScripting;
using UnityEngine;
using ViT.SaveKit.Runtime;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Logic khi khởi động game
/// </summary>
[DefaultExecutionOrder(-1000)]
public partial class GameBootstrap : MonoBehaviour
{
    private ISaveSystem _saveSystem;

    [SerializeField] private Core core;

    [Header("------Manager----------------------------------")] [SerializeField]
    private ProgressManager progressManager;
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private SoundManager soundManager;

    [Header("------Controller----------------------------------")] [SerializeField]
    private ChapterController chapterController;
    [SerializeField] private SessionController sessionController;
    [SerializeField] private ShipController shipController;

    [Header("------Input----------------------------------")] [SerializeField]
    DragInput dragInput;


    private void Awake()
    {
        ResolveSceneReferences();
        RegisterGlobalDi();

        BuildSaveSystem();
    }
    
    private void OnApplicationPause(bool pause)
    {
        if (pause) _saveSystem?.SaveAll();
    }
    
    private void OnApplicationQuit()
    {
        _saveSystem?.SaveAll();
    }

}

// Logic
public partial class GameBootstrap
{
    #region DI

    /// <summary>
    /// Đăng ký vào DI container global
    /// </summary>
    private void ResolveSceneReferences()
    {
        shipController ??= FindFirstObjectByType<ShipController>();
        soundManager ??= FindFirstObjectByType<SoundManager>();
    }

    /// <summary>
    /// Đăng ký vào DI container global
    /// </summary>
    private void RegisterGlobalDiContainer()
    {
        //Injector.GlobalServices.Set<ISaveSystem>(_saveSystem);

        //Injector.GlobalServices.Set(_gameBootstrap);
        Injector.GlobalServices.Set(core);

        Injector.GlobalServices.Set(progressManager);
        Injector.GlobalServices.Set(chapterManager);
        Injector.GlobalServices.Set(cameraManager);
        Injector.GlobalServices.Set(spawnManager);
        Injector.GlobalServices.Set(itemManager);
        if (soundManager.IsUnityNull())
            Injector.GlobalServices.Set(soundManager);

        Injector.GlobalServices.Set(chapterController);
        Injector.GlobalServices.Set(sessionController);

        Injector.GlobalServices.Set(dragInput);

        Injector.GlobalServices.Set<IProgressQuery>(progressManager);
        Injector.GlobalServices.Set<IChapterQuery>(chapterManager);

        Injector.GlobalServices.Set<ISolarObjectFactory>(new AddressablesSolarObjectFactory());
    }
    #endregion

    #region Save

    /// <summary>
    /// Build SaveSystem và register tất cả adapter/module cần save.
    /// </summary>
    private void BuildSaveSystem()
    {
        _saveSystem = SaveKitFactory.CreateLocalJson(Application.persistentDataPath,
            new ProgressSaveAdapter(progressManager),
            new ItemSaveAdapter(itemManager),
            new PlayerStatSaveAdapter(shipController));
    }
    
    #endregion
}

// API
public partial class GameBootstrap
{
    public void RegisterGlobalDi()
    {
        RegisterGlobalDiContainer();
    }
    
    public void SaveNow()
    {
        try
        {
            _saveSystem?.SaveAll();
        }
        catch (System.Exception)
        {
            throw;
        }
    }

    public void LoadNow()
    {
        try
        {
            _saveSystem.LoadAll();
        }
        catch (System.Exception)
        {
            throw;
        }
    }
    
}

#if UNITY_EDITOR
[CustomEditor(typeof(GameBootstrap))]
public class GameBootstrapEditor : Editor
{
    private GameBootstrap _target;

    private void OnEnable()
    {
        _target = (GameBootstrap)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            if (GUILayout.Button("Register Global DI (Editor)"))
            {
                _target.RegisterGlobalDi();
                EditorUtility.SetDirty(_target);
            }
        }
    }
}

#endif