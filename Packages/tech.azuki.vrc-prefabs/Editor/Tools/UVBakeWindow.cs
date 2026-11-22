#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VAP.Editor
{
    internal class UVBakeWindow : EditorWindow
    {
        const string PrefBlenderPath = "AzukiUVBake_BlenderExePath";
        const string PrefDeviceMode = "AzukiUVBake_DeviceMode";
        const string ScriptFileName = "bake_uv.py";

        static readonly string[] DeviceModes = { "Auto", "GPU", "CPU" };

        string blenderPath = "";
        UnityEngine.Object fbxAsset;
        Texture2D texture;

        string[] materials = Array.Empty<string>();
        readonly Dictionary<string, string[]> uvMaps = new Dictionary<string, string[]>();
        string fetchedFbx = "";
        int pickerId = -1;

        int materialIndex = -1;
        int sourceIndex = -1;
        int destIndex = -1;
        int margin = 32;
        int deviceMode;

        readonly StringBuilder log = new StringBuilder();
        readonly object logLock = new object();
        Vector2 logScroll;
        bool scrollToBottom;
        volatile bool logChanged;

        Process bakeProcess;
        bool baking;
        volatile bool bakeExited;
        DateTime bakeStart;
        string bakeOutput;
        Texture2D bakeSource;

        string Material => materialIndex >= 0 && materialIndex < materials.Length ? materials[materialIndex] : "";

        string[] UVs => Material.Length > 0 && uvMaps.TryGetValue(Material, out var uvs) ? uvs : Array.Empty<string>();

        [MenuItem("Tools/VAP/UV Bake Tool")]
        static void ShowWindow()
        {
            var window = GetWindow<UVBakeWindow>("UV Bake Tool");
            window.minSize = new Vector2(420, 520);
        }

        void OnEnable()
        {
            blenderPath = EditorPrefs.GetString(PrefBlenderPath, "");
            if (!File.Exists(blenderPath))
            {
                string found = FindBlender();
                if (found != null)
                    SetBlenderPath(found);
            }
            deviceMode = Mathf.Clamp(EditorPrefs.GetInt(PrefDeviceMode, 0), 0, DeviceModes.Length - 1);
            EditorApplication.update += OnUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
        }

        void SetBlenderPath(string path)
        {
            blenderPath = path;
            EditorPrefs.SetString(PrefBlenderPath, path);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                string newPath = EditorGUILayout.TextField(new GUIContent("Blender", "Path to the Blender executable"), blenderPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string dir = File.Exists(newPath) ? Path.GetDirectoryName(newPath) : "";
                    string picked = EditorUtility.OpenFilePanel("Blender", dir, Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                    if (!string.IsNullOrEmpty(picked))
                        newPath = picked;
                }
                if (GUILayout.Button(new GUIContent("Find", "Look for Blender in the usual install folders and PATH"), GUILayout.Width(50)))
                {
                    string found = FindBlender();
                    if (found != null)
                        newPath = found;
                    else
                        EditorUtility.DisplayDialog("Blender not found", "Couldn't find Blender. Pick the Blender executable with the ... button.", "OK");
                }
                if (newPath != blenderPath)
                    SetBlenderPath(newPath);
            }

            if (!File.Exists(ScriptPath()))
                EditorGUILayout.HelpBox($"{ScriptFileName} is missing from:\n{Path.GetDirectoryName(ScriptPath())}", MessageType.Warning);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFbxField();
                if (fbxAsset != null && GUILayout.Button(new GUIContent("Refresh", "Read the materials and UV maps again"), GUILayout.Width(60)))
                    Refresh();
            }

            AutoFetch();

            if (fbxAsset == null && !baking)
            {
                DrawLog();
                return;
            }

            DrawMaterials();
            DrawUVPopup(new GUIContent("From UV", "UV map the texture is laid out on"), ref sourceIndex);
            DrawUVPopup(new GUIContent("To UV", "UV map to bake the texture onto"), ref destIndex);
            texture = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Texture", "Texture laid out on From UV. The result is saved next to it, named <texture>_<model>_<To UV>, with the same size, format and import settings"), texture, typeof(Texture2D), false);
            margin = EditorGUILayout.IntSlider(new GUIContent("Margin (px)", "How far the baked color extends past each UV island. Raise it if islands get dark edges"), margin, 1, 256);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Bake Device", "Auto uses the GPU when Blender finds one. Use CPU if the result comes out patchy or purple"));
                int newMode = GUILayout.Toolbar(deviceMode, DeviceModes);
                if (newMode != deviceMode)
                {
                    deviceMode = newMode;
                    EditorPrefs.SetInt(PrefDeviceMode, deviceMode);
                }
            }

            EditorGUILayout.Space(10);
            string problem = Problem();
            using (new EditorGUI.DisabledScope(baking || problem != null))
                if (GUILayout.Button(baking ? "Baking..." : "Bake", GUILayout.Height(32)))
                    StartBake();

            if (baking)
            {
                EditorGUILayout.HelpBox($"Baking ({(DateTime.Now - bakeStart).TotalSeconds:0.0}s)", MessageType.Info);
                if (GUILayout.Button("Cancel"))
                    CancelBake();
            }
            else if (problem != null)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);

            DrawLog();
        }

        void DrawLog()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            string text;
            lock (logLock)
                text = log.ToString();
            float width = Mathf.Max(position.width - 40f, 50f);
            float height = Mathf.Max(EditorStyles.textArea.CalcHeight(new GUIContent(text), width), 20f);
            if (scrollToBottom && Event.current.type == EventType.Layout)
            {
                logScroll.y = height;
                scrollToBottom = false;
            }
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(text, EditorStyles.textArea, GUILayout.Height(height), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Clear Log"))
            {
                lock (logLock)
                    log.Clear();
                logScroll = Vector2.zero;
            }
        }

        string Problem()
        {
            if (!File.Exists(blenderPath)) return "Set the path to the Blender executable.";
            if (!File.Exists(ScriptPath())) return $"{ScriptFileName} is missing.";
            if (fbxAsset == null) return "Pick an FBX model.";
            if (materials.Length == 0) return "This model has no materials.";
            if (materialIndex < 0) return "Pick a material.";
            if (UVs.Length < 2) return "This material needs at least two UV maps.";
            if (sourceIndex < 0 || sourceIndex >= UVs.Length) return "Pick a From UV.";
            if (destIndex < 0 || destIndex >= UVs.Length) return "Pick a To UV.";
            if (sourceIndex == destIndex) return "From UV and To UV must be different.";
            if (texture == null) return "Pick a texture.";
            return null;
        }

        void DrawFbxField()
        {
            var rect = EditorGUILayout.GetControlRect();
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var e = Event.current;
            var fieldRect = EditorGUI.PrefixLabel(rect, new GUIContent("Model", "FBX model that has both UV maps"));
            var pickerRect = new Rect(fieldRect.xMax - 19f, fieldRect.y, 19f, fieldRect.height);
            if (e.type == EventType.MouseDown && e.button == 0 && pickerRect.Contains(e.mousePosition))
            {
                pickerId = id;
                EditorGUIUtility.ShowObjectPicker<GameObject>(fbxAsset as GameObject, false, "glob:\"*.fbx\"", id);
                e.Use();
            }
            var picked = EditorGUI.ObjectField(fieldRect, fbxAsset, typeof(GameObject), false);
            if (picked != fbxAsset)
                SetFbx(picked);
            if (e.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == pickerId)
            {
                SetFbx(EditorGUIUtility.GetObjectPickerObject());
                Repaint();
            }
        }

        void SetFbx(UnityEngine.Object candidate)
        {
            if (candidate != null && !AssetDatabase.GetAssetPath(candidate).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                Log($"{candidate.name} is not an FBX file.");
                return;
            }
            fbxAsset = candidate;
        }

        static string FullPath(UnityEngine.Object asset)
        {
            string assetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : "";
            return string.IsNullOrEmpty(assetPath) ? "" : Path.GetFullPath(FileUtil.GetPhysicalPath(assetPath));
        }

        string OutputAssetPath()
        {
            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath) || fbxAsset == null || destIndex < 0 || destIndex >= UVs.Length)
                return "";
            string dir = Path.GetDirectoryName(texturePath)?.Replace('\\', '/');
            string name = $"{Path.GetFileNameWithoutExtension(texturePath)}_{FileName(fbxAsset.name)}_{FileName(UVs[destIndex])}{Path.GetExtension(texturePath)}";
            return string.IsNullOrEmpty(dir) ? name : dir + "/" + name;
        }

        static string FileName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars().Concat(new[] { ' ', '/', '\\', ':', '*', '?', '"', '<', '>', '|' }))
                s = s.Replace(c, '_');
            return s;
        }

        string ScriptPath()
        {
            string scriptAsset = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string dir = Path.GetDirectoryName(Path.GetFullPath(FileUtil.GetPhysicalPath(scriptAsset))) ?? "";
            return Path.Combine(dir, ScriptFileName);
        }

        void AutoFetch()
        {
            string fbxPath = FullPath(fbxAsset);
            if (fbxAsset != null && File.Exists(fbxPath))
            {
                if (fbxPath != fetchedFbx)
                    Fetch(fbxPath);
            }
            else if (fetchedFbx.Length > 0)
                ClearFetched();
        }

        void Refresh()
        {
            string fbxPath = FullPath(fbxAsset);
            if (!File.Exists(fbxPath))
                return;
            string material = Material;
            string source = sourceIndex >= 0 && sourceIndex < UVs.Length ? UVs[sourceIndex] : null;
            string dest = destIndex >= 0 && destIndex < UVs.Length ? UVs[destIndex] : null;
            Fetch(fbxPath);
            int index = Array.IndexOf(materials, material);
            if (index < 0)
                return;
            materialIndex = index;
            if (source != null && Array.IndexOf(UVs, source) >= 0)
                sourceIndex = Array.IndexOf(UVs, source);
            if (dest != null && Array.IndexOf(UVs, dest) >= 0)
                destIndex = Array.IndexOf(UVs, dest);
        }

        void ClearFetched()
        {
            materials = Array.Empty<string>();
            uvMaps.Clear();
            fetchedFbx = "";
            materialIndex = sourceIndex = destIndex = -1;
        }

        void Fetch(string fbxPath)
        {
            ClearFetched();
            fetchedFbx = fbxPath;
            if (!File.Exists(blenderPath) || !File.Exists(ScriptPath()))
            {
                Log("Set the Blender path to read the model's materials.");
                return;
            }

            try
            {
                using (var process = Process.Start(StartInfo("--list-all", fbxPath)))
                {
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    string stdout = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    string stderr = stderrTask.Result;

                    materials = Section(stdout, "MATLIST_START", "MATLIST_END");
                    foreach (string m in materials)
                        uvMaps[m] = Section(stdout, "UVLIST_START:" + m, "UVLIST_END:" + m);

                    if (materials.Length == 0)
                    {
                        Log("No materials found. Blender output:");
                        Log(stdout);
                        if (!string.IsNullOrWhiteSpace(stderr))
                            Log(stderr);
                    }
                    else
                        foreach (string m in materials)
                            Log($"{m}: {(uvMaps[m].Length > 0 ? string.Join(", ", uvMaps[m]) : "no UV maps")}");
                }
            }
            catch (Exception ex)
            {
                Log("Couldn't run Blender: " + ex.Message);
                materials = Array.Empty<string>();
                uvMaps.Clear();
            }

            if (materials.Length > 0)
            {
                materialIndex = 0;
                sourceIndex = UVs.Length > 0 ? 0 : -1;
                destIndex = UVs.Length > 1 ? UVs.Length - 1 : -1;
            }
            Repaint();
        }

        void DrawMaterials()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Material");
                if (materials.Length == 0)
                {
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Toolbar(-1, new[] { "None" });
                    return;
                }
                int index = GUILayout.Toolbar(materialIndex, materials);
                if (index == materialIndex)
                    return;
                string source = sourceIndex >= 0 && sourceIndex < UVs.Length ? UVs[sourceIndex] : null;
                string dest = destIndex >= 0 && destIndex < UVs.Length ? UVs[destIndex] : null;
                materialIndex = index;
                sourceIndex = source != null ? Array.IndexOf(UVs, source) : -1;
                destIndex = dest != null ? Array.IndexOf(UVs, dest) : -1;
            }
        }

        void DrawUVPopup(GUIContent label, ref int index)
        {
            var uvs = UVs;
            if (uvs.Length == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup(label, 0, new[] { new GUIContent("") });
                return;
            }
            index = EditorGUILayout.Popup(label, index, uvs.Select(u => new GUIContent(u)).ToArray());
        }

        static string[] Section(string text, string start, string end)
        {
            var result = new List<string>();
            bool inside = false;
            foreach (string raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                if (line == start)
                    inside = true;
                else if (line == end)
                    inside = false;
                else if (inside && line.Length > 0)
                    result.Add(line);
            }
            return result.ToArray();
        }

        static string FindBlender()
        {
            var candidates = new List<string>();
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                foreach (var root in new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                }.Where(r => !string.IsNullOrEmpty(r)).Distinct())
                {
                    string foundation = Path.Combine(root, "Blender Foundation");
                    if (Directory.Exists(foundation))
                    {
                        try
                        {
                            candidates.AddRange(Directory.GetDirectories(foundation)
                                .OrderByDescending(VersionOf)
                                .Select(d => Path.Combine(d, "blender.exe")));
                        }
                        catch (Exception)
                        {
                        }
                    }
                    candidates.Add(Path.Combine(root, "Steam", "steamapps", "common", "Blender", "blender.exe"));
                }
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
                candidates.Add("/Applications/Blender.app/Contents/MacOS/Blender");
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                candidates.AddRange(new[]
                {
                    "/usr/bin/blender",
                    "/usr/local/bin/blender",
                    "/snap/bin/blender",
                    "/opt/blender/blender",
                    Path.Combine(home, ".local", "bin", "blender"),
                    Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Blender", "blender"),
                    Path.Combine(home, ".steam", "steam", "steamapps", "common", "Blender", "blender"),
                    Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps", "common", "Blender", "blender"),
                });
            }

            string exe = Application.platform == RuntimePlatform.WindowsEditor ? "blender.exe" : "blender";
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                try
                {
                    candidates.Add(Path.Combine(dir.Trim('"'), exe));
                }
                catch (ArgumentException)
                {
                }
            }

            return candidates.FirstOrDefault(File.Exists);
        }

        static Version VersionOf(string dir)
        {
            var match = Regex.Match(Path.GetFileName(dir), @"\d+(\.\d+)+");
            return match.Success && Version.TryParse(match.Value, out var v) ? v : new Version(0, 0);
        }

        static string Quote(string arg)
        {
            var sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }
                sb.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                slashes = 0;
                sb.Append(c);
            }
            sb.Append('\\', slashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        ProcessStartInfo StartInfo(params string[] args)
        {
            var info = new ProcessStartInfo
            {
                FileName = blenderPath,
                Arguments = "--background --factory-startup --python " + Quote(ScriptPath()) + " -- " + string.Join(" ", args.Select(Quote)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            info.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            return info;
        }

        void StartBake()
        {
            lock (logLock)
                log.Clear();
            string outputAsset = OutputAssetPath();
            string outputPath = Path.Combine(Path.GetDirectoryName(FullPath(texture)) ?? "", Path.GetFileName(outputAsset));
            string source = UVs[sourceIndex];
            string dest = UVs[destIndex];
            string device = DeviceModes[deviceMode].ToUpperInvariant();

            Log($"Baking {Material}: {source} to {dest} ({device})");
            Log("Output: " + outputAsset);

            bakeOutput = outputAsset;
            bakeSource = texture;
            bakeExited = false;
            bakeStart = DateTime.Now;
            bakeProcess = new Process
            {
                StartInfo = StartInfo(FullPath(fbxAsset), Material, source, dest, FullPath(texture), outputPath, margin.ToString(), device),
                EnableRaisingEvents = true,
            };
            bakeProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
            bakeProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
            bakeProcess.Exited += (s, e) => bakeExited = true;

            try
            {
                bakeProcess.Start();
                bakeProcess.BeginOutputReadLine();
                bakeProcess.BeginErrorReadLine();
                baking = true;
            }
            catch (Exception ex)
            {
                Log("Couldn't start Blender: " + ex.Message);
                bakeProcess.Dispose();
                bakeProcess = null;
            }
        }

        void CancelBake()
        {
            try
            {
                if (bakeProcess != null && !bakeProcess.HasExited)
                    bakeProcess.Kill();
            }
            catch (Exception ex)
            {
                Log("Couldn't stop Blender: " + ex.Message);
            }
            Log("Bake cancelled.");
            FinishBake(false);
        }

        void OnUpdate()
        {
            if (baking && bakeExited)
            {
                int code = -1;
                try
                {
                    bakeProcess.WaitForExit();
                    code = bakeProcess.ExitCode;
                }
                catch (Exception)
                {
                }
                Log(code == 0 ? "Bake finished." : $"Blender exited with code {code}.");
                FinishBake(code == 0);
            }
            if (logChanged || baking)
            {
                logChanged = false;
                Repaint();
            }
        }

        void FinishBake(bool succeeded)
        {
            baking = false;
            bakeExited = false;
            bakeProcess?.Dispose();
            bakeProcess = null;
            if (succeeded)
                ImportOutput(bakeOutput, bakeSource);
        }

        void ImportOutput(string outputAsset, Texture2D source)
        {
            if (string.IsNullOrEmpty(outputAsset))
                return;
            AssetDatabase.ImportAsset(outputAsset, ImportAssetOptions.ForceUpdate);
            var from = source != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as TextureImporter : null;
            var to = AssetImporter.GetAtPath(outputAsset) as TextureImporter;
            if (from == null || to == null)
            {
                Log("Saved, but the import settings couldn't be copied.");
                return;
            }
            EditorUtility.CopySerialized(from, to);
            to.SaveAndReimport();
            Log("Copied the texture's import settings.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(outputAsset));
        }

        void Log(string line)
        {
            lock (logLock)
                log.AppendLine(line);
            scrollToBottom = true;
            logChanged = true;
        }
    }
}
#endif
