using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SFB;
using TMPro;

public class DockManager : MonoBehaviour
{
    public Button addButton;
    public Transform appContainer;
    public GameObject appButtonPrefab;
    public Button removeButton;

    private string sharedPath;
    private Dictionary<string, string> dockApps = new();

    void Start()
    {
        sharedPath = Path.Combine(Application.persistentDataPath, "vc_apps.json");
        LoadDockApps();

        addButton.onClick.AddListener(AddAppToDock);
        removeButton.onClick.AddListener(RemoveLastApp);
    }

    void AddAppToDock()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel("Select EXE", "", "exe", false);
        if (paths.Length > 0)
        {
            string exePath = paths[0];
            string name = Path.GetFileNameWithoutExtension(exePath);

            if (!dockApps.ContainsKey(name))
            {
                dockApps[name] = exePath;
                SaveDockApps();
                CreateAppButton(name, exePath);
            }
        }
    }

    void CreateAppButton(string name, string path)
    {
        GameObject button = Instantiate(appButtonPrefab, appContainer);

        // Extract icon
        Texture2D tex = IconExtractor.GetIconFromExe(path);
        if (tex != null)
        {
            Sprite iconSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            button.GetComponentInChildren<Image>().sprite = iconSprite;
        }

        // Remove text (if any)
        TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null) tmpText.text = "";

        button.GetComponent<Button>().onClick.AddListener(() =>
        {
            System.Diagnostics.Process.Start(path);
        });
    }


    void LoadDockApps()
    {
        if (File.Exists(sharedPath))
        {
            string json = File.ReadAllText(sharedPath);
            dockApps = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            foreach (var kvp in dockApps)
            {
                CreateAppButton(kvp.Key, kvp.Value);
            }
        }
    }

    void SaveDockApps()
    {
        string json = JsonConvert.SerializeObject(dockApps, Formatting.Indented);
        File.WriteAllText(sharedPath, json);
    }

    public void RemoveLastApp()
    {
        if (dockApps.Count == 0) return;

        // Get last added key
        string lastKey = null;
        foreach (var key in dockApps.Keys)
        {
            lastKey = key;
        }

        if (lastKey != null)
        {
            dockApps.Remove(lastKey);
            SaveDockApps();

            // Remove last button from container
            if (appContainer.childCount > 0)
            {
                Transform lastChild = appContainer.GetChild(appContainer.childCount - 1);
                Destroy(lastChild.gameObject);
            }
        }
    }
}
