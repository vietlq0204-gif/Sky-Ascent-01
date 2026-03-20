using UnityEngine;
using Save.Core;
using Account;
using Save.IO;
//using Progress.Save;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// logic khi khởi động game
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameBootstrap : MonoBehaviour
{
    ISaveSystem _saveSystem;

    //[SerializeField] GameBootstrap _gameBootstrap;
    [SerializeField] Core _core;

    [Header("------Manager----------------------------------")]
    [SerializeField] ProgressManager _progressManager;
    [SerializeField] ChapterManager _chapterManager;
    [SerializeField] CameraManager _cameraManager;
    [SerializeField] SpawnManager _spawnManager;
    [SerializeField] ItemManager _itemManager;
    [SerializeField] SoundManager _soundManager;

    [Header("------Controller----------------------------------")]
    [SerializeField] ChapterController _chapterController;
    [SerializeField] SessionController _sessionController;
    [SerializeField] ShipController _shipController;
    [Header("------Input----------------------------------")]
    [SerializeField] DragInput _dragInput;

    #region Unity lifecycle

    private void Awake()
    {
        ResolveSceneReferences();
        RegistorGlobalDI();

        BuildSaveSystem();
    }

    /// <summary>
    /// rào save khi pause ứng dụng
    /// </summary>
    /// <param name="pause"></param>
    private void OnApplicationPause(bool pause)
    {
        if (pause) _saveSystem?.SaveAll();
    }

    /// <summary>
    /// rào lưu khi thoát ứng dụng
    /// </summary>
    private void OnApplicationQuit()
    {
        _saveSystem?.SaveAll();
    }

    #endregion

    #region Public API

    public void RegistorGlobalDI()
    {
        RegistorGlobalDIContainer();
    }

    /// <summary>
    ///save ngay.
    /// </summary>
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

    #endregion

    #region Logic

    /// <summary>
    /// Đăng ký vào DI container global
    /// </summary>
    private void ResolveSceneReferences()
    {
        _shipController ??= FindFirstObjectByType<ShipController>();
        _soundManager ??= FindFirstObjectByType<SoundManager>();
    }

    /// <summary>
    /// Đăng ký vào DI container global
    /// </summary>
    private void RegistorGlobalDIContainer()
    {
        //Injector.GlobalServices.Set<ISaveSystem>(_saveSystem);

        //Injector.GlobalServices.Set(_gameBootstrap);
        Injector.GlobalServices.Set(_core);

        Injector.GlobalServices.Set(_progressManager);
        Injector.GlobalServices.Set(_chapterManager);
        Injector.GlobalServices.Set(_cameraManager);
        Injector.GlobalServices.Set(_spawnManager);
        Injector.GlobalServices.Set(_itemManager);
        if (_soundManager != null)
            Injector.GlobalServices.Set(_soundManager);

        Injector.GlobalServices.Set(_chapterController);
        Injector.GlobalServices.Set(_sessionController);

        Injector.GlobalServices.Set(_dragInput);

        Injector.GlobalServices.Set<IProgressQuery>(_progressManager);
        Injector.GlobalServices.Set<IChapterQuery>(_chapterManager);

        Injector.GlobalServices.Set<ISolarObjectFactory>(new AddressablesSolarObjectFactory());

    }

    /// <summary>
    /// Đăng ký tất cả adapter/module cần save vào SaveSystem
    /// </summary>
    private void RegistorAdapterSaveable()
    {
        _saveSystem.Register(new ProgressSaveAdapter(_progressManager));
        _saveSystem.Register(new ItemSaveAdapter(_itemManager));

        if (_shipController != null)
        {
            _saveSystem.Register(new PlayerStatSaveAdapter(_shipController));
        }
    }

    /// <summary>
    /// Build SaveSystem và register tất cả adapter/module cần save.
    /// </summary>
    private void BuildSaveSystem()
    {
        // build core save deps (POCO) (cần fix)
        var account = new AccountContext("guest_001");

        IFolderProvider folders = new FolderProvider(Application.persistentDataPath);
        IFileHandler files = new JsonFileHandlerNewtonsoft();
        ISaveStore store = new LocalJsonSaveStore(folders, files);

        // build SaveSystem
        _saveSystem = new SaveSystem(account, store);

        RegistorAdapterSaveable();
    }

    #endregion

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
                _target.RegistorGlobalDI();
                EditorUtility.SetDirty(_target);
            }
        }
    }
}

#endif
