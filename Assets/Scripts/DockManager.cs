using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SFB; // Standalone File Browser (imported from GitHub)
using TMPro;


public class DockManager : MonoBehaviour
{
    public Button addButton;
    public Transform appContainer;
    public GameObject appButtonPrefab;
    public Button removeButton;


    private string savePath;
    private List<AppData> dockApps = new List<AppData>();

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "dock_apps.json");
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

            AppData app = new AppData { name = name, exePath = exePath };
            dockApps.Add(app);
            SaveDockApps();
            CreateAppButton(app);
        }
    }

    void CreateAppButton(AppData app)
    {
        GameObject button = Instantiate(appButtonPrefab, appContainer);
        button.GetComponentInChildren<TextMeshProUGUI>().text = app.name;
        button.GetComponent<Button>().onClick.AddListener(() => {
            System.Diagnostics.Process.Start(app.exePath);
        });
    }

    void LoadDockApps()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            dockApps = JsonConvert.DeserializeObject<List<AppData>>(json);
            foreach (var app in dockApps)
            {
                CreateAppButton(app);
            }
        }
    }

    void SaveDockApps()
    {
        string json = JsonConvert.SerializeObject(dockApps, Formatting.Indented);
        File.WriteAllText(savePath, json);
    }

    public void RemoveLastApp()
    {
        if (dockApps.Count == 0) return;

        // Remove from list
        var lastApp = dockApps[dockApps.Count - 1];
        dockApps.RemoveAt(dockApps.Count - 1);
        SaveDockApps();

        // Remove last button from container
        if (appContainer.childCount > 0)
        {
            Transform lastChild = appContainer.GetChild(appContainer.childCount - 1);
            Destroy(lastChild.gameObject);
        }
    }

}
