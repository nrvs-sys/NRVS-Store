using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NRVS.Store
{

    /// <summary>
    /// Static API for the Generic StoreBehavior class.
    /// </summary>
    public static class StoreBehavior
    {
        #region Platform Directory Path Methods

        public const string DefaultPlatformDirectory = "";
        public const string EditorPlatformDirectory = "Editor";

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task<string> GetPlatformDirectoryPathAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#if UNITY_EDITOR
        => EditorPlatformDirectory;
#elif PLAYERPLATFORM_STEAM
    { 
        SteamManager steamManager = null;

        while (!Ref.TryGet(out steamManager) || !steamManager.isLoggedIn)
        {
            await Task.Yield();
        }

        return steamManager.accountID.ToString();
    }
#else
    => DefaultPlatformDirectory;
#endif

        public static string GetPlatformDirectoryPath()
#if UNITY_EDITOR
        => EditorPlatformDirectory;
#elif PLAYERPLATFORM_STEAM
    { 
        SteamManager steamManager = null;

        if (!Ref.TryGet(out steamManager) || !steamManager.isLoggedIn)
        {
            Debug.LogError($"Getting game data directory path for Steam user failed!");
            return "";
        }

        return steamManager.accountID.ToString();
    }
#else
    => DefaultPlatformDirectory;
#endif

        #endregion
    }

    /// <summary>
    /// Generic Store Behavior for saving and loading data to the platform's file system. All files are saved in a relative directory based on the platform (e.g. Editor, Steam, etc.).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class StoreBehavior<T> : ScriptableObject
    {
        public enum StoreType
        {
            Json
        }

        #region Serialized Fields

        [SerializeField]
        StoreType storeType;

        [Header("Store Settings")]

        bool showFileInspectorFields => storeType == StoreType.Json;

        [SerializeField, ShowIf(nameof(showFileInspectorFields))]
        string fileName = "Untitled";

        [SerializeField, ShowIf(nameof(showFileInspectorFields)), Tooltip("If set, this will replace the default extension for the store type. Don't lead the extension with a `.`.")]
        string fileExtensionOverride;

        [Header("Debug Tools")]

        [SerializeField, Tooltip("The relative directory used when Saving/Loading stored values in the inspector.")]
        string inspectorFileDirectory = StoreBehavior.EditorPlatformDirectory;
        [SerializeField, Tooltip("Use the Load and Save Buttons to view and edit the stored values in the inspector.")]
        protected T inspectorValue = default(T);

        #endregion

        #region Load Methods

        /// <summary>
        /// Loads the data from a file in the given (relative) directory using the configured storage format.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="directory">The relative directory where the file will be loaded from. Must be a valid and accessible path.</param>
        /// <returns>`true` if data was successfully loaded</returns>
        public bool Load(out T value, string directory)
        {
            value = default;

            var fileNameWithExt = GetFileNameWithExtension(fileName, storeType);
            var path = directory.IsNullOrEmpty() ? fileNameWithExt : directory + System.IO.Path.AltDirectorySeparatorChar + fileNameWithExt;

            if (!FileUtility.Exists(path))
            {
                Debug.LogWarning($"Store Behavior - Store not found for {path}. Load unsuccessful.");
                return false;
            }

            if (!FileUtility.ReadText(path, out var fileContents))
                return false;

            switch (storeType)
            {
                case StoreType.Json:
                    var data = JsonUtility.FromJson<T>(fileContents);

                    if (data is IStorable storable && storable.GetStoreVersion() < storable.GetLatestStoreVersion())
                        storable.UpdateStoreVersion();

                    value = data;
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Loads the data from a file in the (relative) root directory using the configured storage format.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>`true` if data was succesfully loaded</returns>
        public bool Load(out T value) => Load(out value, null);

        /// <summary>
        /// Loads an object of type <typeparamref name="T"/> from the specified directory.
        /// </summary>
        /// <param name="directory">The relative directory from which to load the object.</param>
        /// <returns>The loaded object of type <typeparamref name="T"/>.</returns>
        public T Load(string directory)
        {
            Load(out var value, directory);
            return value;
        }

        /// <summary>
        /// Loads an object of type <typeparamref name="T"/> from the (relative) root directory.
        /// </summary>
        /// <returns></returns>
        public T Load()
        {
            Load(out var value);
            return value;
        }


        public async Task<T> LoadAsync(string directory)
        {
            var fileNameWithExt = GetFileNameWithExtension(fileName, storeType);

            var path = directory.IsNullOrEmpty() ? fileNameWithExt : System.IO.Path.Combine(directory, fileNameWithExt);

            if (!FileUtility.Exists(path))
            {
                Debug.LogWarning($"Store Behavior - Store not found for {path}. Load unsuccessful.");
                return default;
            }

            var results = await FileUtility.ReadTextAsync(path);

            if (!results.success)
            {
                Debug.LogWarning($"Store Behavior - Failed to read store for {path}. Load unsuccessful.");
                return default;
            }

            var fileContents = results.fileContents;

            switch (storeType)
            {
                case StoreType.Json:
                    var data = JsonUtility.FromJson<T>(fileContents);
                    if (data is IStorable storable && storable.GetStoreVersion() < storable.GetLatestStoreVersion())
                        storable.UpdateStoreVersion();
                    return data;
            }
            return default;
        }

        public async Task<T> LoadAsync()
        {
            return await LoadAsync(null);
        }

        public T LoadFromPlatformDirectory()
        {
            var directory = StoreBehavior.GetPlatformDirectoryPath();
            return Load(directory);
        }

        public async Task<T> LoadFromPlatformDirectoryAsync()
        {
            var directory = await StoreBehavior.GetPlatformDirectoryPathAsync();
            return await LoadAsync(directory);
        }

        #endregion

        #region Save Methods

        /// <summary>
        /// Saves the specified data to a file in the (relative) root directory using the configured storage format.
        /// </summary>
        /// <param name="data">The data to be saved. Cannot be null.</param>
        public void Save(T data) => Save(data, null);

        /// <summary>
        /// Saves the specified data to a file in the given (relative) directory using the configured storage format.
        /// </summary>
        /// <param name="data">The data to be saved.</param>
        /// <param name="directory">The relative directory where the file will be saved. Must be a valid and accessible path.</param>
        public void Save(T data, string directory)
        {
            var fileNameWithExt = GetFileNameWithExtension(fileName, storeType);
            var path = directory.IsNullOrEmpty() ? fileNameWithExt : System.IO.Path.Combine(directory, fileNameWithExt);

            switch (storeType)
            {
                case StoreType.Json:
                    var json = JsonUtility.ToJson(data);
                    FileUtility.WriteText(path, json);
                    break;
            }
        }

        public async Task SaveAsync(T data) => await SaveAsync(data, null);

        public async Task SaveAsync(T data, string directory)
        {
            var fileNameWithExt = GetFileNameWithExtension(fileName, storeType);
            var path = directory.IsNullOrEmpty() ? fileNameWithExt : System.IO.Path.Combine(directory, fileNameWithExt);
            switch (storeType)
            {
                case StoreType.Json:
                    var json = JsonUtility.ToJson(data);
                    await FileUtility.WriteTextAsync(path, json);
                    break;
            }
        }

        public void SaveToPlatformDirectory(T data)
        {
            var directory = StoreBehavior.GetPlatformDirectoryPath();
            Save(data, directory);
        }

        public async Task SaveToPlatformDirectoryAsync(T data)
        {
            var directory = await StoreBehavior.GetPlatformDirectoryPathAsync();
            await SaveAsync(data, directory);
        }

        #endregion

        #region Delete Methods

        /// <summary>
        /// Deletes the store file from the given (relative) directory.
        /// </summary>
        /// <param name="directory"></param>
        public void DeleteStore(string directory)
        {
            var fileNameWithExt = GetFileNameWithExtension();
            var path = directory.IsNullOrEmpty() ? fileNameWithExt : System.IO.Path.Combine(directory, fileNameWithExt);

            if (!FileUtility.Exists(path))
            {
                Debug.LogWarning($"Store Behavior - Store not found for {path}. Delete unsuccessful.");
                return;
            }

            FileUtility.Delete(path);

            Debug.Log($"Store Behavior - Deleted Store for file at {path}");
        }

        /// <summary>
        /// Deletes the store file from the (relative) root directory.
        /// </summary>
        public void DeleteStore() => DeleteStore(null);

        public void DeleteStoreFromPlatformDirectory()
        {
            var directory = StoreBehavior.GetPlatformDirectoryPath();

            DeleteStore(directory);
        }

        public async void DeleteStoreFromPlatformDirectoryAsync()
        {
            var directory = await StoreBehavior.GetPlatformDirectoryPathAsync();
            DeleteStore(directory);
        }

        #endregion

        #region Inspector Methods

        [Button("Load Store")]
        void LoadInspectorValue()
        {
            Load(out inspectorValue, inspectorFileDirectory);
        }

        [Button("Save Store")]
        void SaveInspectorValue()
        {
            Save(inspectorValue, inspectorFileDirectory);
        }


        [Button("Delete Store")]
        void DeleteStoreButton()
        {
            var fileNameWithExt = GetFileNameWithExtension();
            var path = inspectorFileDirectory.IsNullOrEmpty() ? fileNameWithExt : System.IO.Path.Combine(inspectorFileDirectory, fileNameWithExt);

#if UNITY_EDITOR

            if (!UnityEditor.EditorUtility.DisplayDialog(
                $"Delete Store at {path}?",
                $"Are you sure you want to delete the Store at {path}?",
                "Confirm", "Cancel"))
                return;

#endif

            DeleteStore(path);

            // Reload the store
            LoadInspectorValue();
        }

        #endregion

        public string GetFileNameWithExtension() => GetFileNameWithExtension(fileName, storeType);

        string GetFileNameWithExtension(string fileName, StoreType storeType)
        {
            if (!string.IsNullOrEmpty(fileExtensionOverride))
                return $"{fileName}.{fileExtensionOverride}";

            switch (storeType)
            {
                case StoreType.Json:
                    return $"{fileName}.json";
            }

            return fileName;
        }
    }
}
