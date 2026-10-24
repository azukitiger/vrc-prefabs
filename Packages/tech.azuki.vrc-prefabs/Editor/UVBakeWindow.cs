
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Azuki.Tools
{
    public class UVBakeWindow : EditorWindow
    {
        private const string PrefBlenderPath = "AzukiUVBake_BlenderExePath";
        private const string PrefDeviceMode = "AzukiUVBake_DeviceMode";
        private const string ScriptFileName = "bake_uv.py";

        private static readonly string[] DeviceModeLabels = { "Auto", "GPU", "CPU" };

        private string blenderExePath = "";
        private UnityEngine.Object fbxAsset;
        private Texture2D textureAsset;

        private string[] availableMaterials = Array.Empty<string>();
        private readonly Dictionary<string, string[]> uvMapsByMaterial = new Dictionary<string, string[]>();
        private string lastFetchedFbx = "";

        private int fbxPickerControlID = -1;

        private int materialIndex = -1;
        private int sourceUVIndex = -1;
        private int destUVIndex = -1;
        private int marginPx = 32;
        private int deviceModeIndex = 0;

        private readonly StringBuilder logBuilder = new StringBuilder();
        private readonly object logLock = new object();
        private Vector2 logScroll;
        private bool scrollLogToBottom;
        private Process bakeProcess;
        private volatile bool isBaking = false;
        private DateTime bakeStartTime;

        private string SelectedMaterialName =>
            (materialIndex >= 0 && materialIndex < availableMaterials.Length) ? availableMaterials[materialIndex] : "";

        private string[] CurrentUVs =>
            (!string.IsNullOrEmpty(SelectedMaterialName) && uvMapsByMaterial.TryGetValue(SelectedMaterialName, out var uvs))
                ? uvs : Array.Empty<string>();

        [MenuItem("Tools/VAP/UV Bake Tool")]
        public static void ShowWindow()
        {
            var win = GetWindow<UVBakeWindow>("UV Bake Tool");
            win.minSize = new Vector2(420, 520);
        }

        private void OnEnable()
        {
            blenderExePath = EditorPrefs.GetString(PrefBlenderPath, "");
            if (string.IsNullOrEmpty(blenderExePath) || !File.Exists(blenderExePath))
            {
                string detected = AutoDetectBlenderPath();
                if (!string.IsNullOrEmpty(detected))
                {
                    blenderExePath = detected;
                    EditorPrefs.SetString(PrefBlenderPath, blenderExePath);
                }
            }

            deviceModeIndex = EditorPrefs.GetInt(PrefDeviceMode, 0);
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollRepaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Blender UV Rebake", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Imports an FBX headlessly in Blender and rebakes a texture from one UV map to another. " +
                "Output is saved next to the source texture, suffixed with the FBX name and target UV map name, " +
                "matching the source's resolution and file format.",
                MessageType.None);

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            DrawPathField("Blender", ref blenderExePath, extension: GetBlenderFilePickerExtension(),
                onChanged: () => EditorPrefs.SetString(PrefBlenderPath, blenderExePath));
            if (GUILayout.Button("Auto-detect", GUILayout.Width(90)))
            {
                string detected = AutoDetectBlenderPath();
                if (!string.IsNullOrEmpty(detected))
                {
                    blenderExePath = detected;
                    EditorPrefs.SetString(PrefBlenderPath, blenderExePath);
                }
                else
                {
                    EditorUtility.DisplayDialog("Blender not found",
                        "Couldn't automatically locate Blender. Please browse to the Blender executable manually.", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();

            string scriptPath = GetScriptPath();
            if (!File.Exists(scriptPath))
            {
                EditorGUILayout.HelpBox(
                    $"bake_uv.py not found next to this script. Place it at:\n{scriptPath}",
                    MessageType.Warning);
            }

            bool showRest = fbxAsset != null || isBaking;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            DrawFbxField();
            if (showRest)
            {
                using (new EditorGUI.DisabledScope(fbxAsset == null))
                {
                    if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                    {
                        RefreshAll();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            MaybeAutoFetch();

            if (!showRest)
            {
                return;
            }

            DrawMaterialPopup();
            DrawUVPopup("From UV", ref sourceUVIndex);
            DrawUVPopup("To UV", ref destUVIndex);

            textureAsset = (Texture2D)EditorGUILayout.ObjectField("Texture", textureAsset, typeof(Texture2D), false);

            marginPx = EditorGUILayout.IntSlider(
                new GUIContent("Margin (px)", "How far baked color dilates past each UV island's edge. " +
                    "Increase this if unbaked areas show up as hard black borders around your UV shapes " +
                    "(bigger textures generally need a bigger margin)."),
                marginPx, 1, 256);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bake Device", GUILayout.Width(EditorGUIUtility.labelWidth));
            int newDeviceIdx = GUILayout.Toolbar(deviceModeIndex, DeviceModeLabels);
            EditorGUILayout.EndHorizontal();
            if (newDeviceIdx != deviceModeIndex)
            {
                deviceModeIndex = newDeviceIdx;
                EditorPrefs.SetInt(PrefDeviceMode, deviceModeIndex);
            }
            if (deviceModeIndex != 2)
            {
                EditorGUILayout.HelpBox(
                    "AMD/HIP baking has known corruption bugs (patchy purple/garbled output) on some " +
                    "driver/Blender combinations. If you see that, switch this to CPU.",
                    MessageType.None);
            }

            EditorGUILayout.Space(14);
            using (new EditorGUI.DisabledScope(isBaking || !CanBake()))
            {
                if (GUILayout.Button(isBaking ? "Baking..." : "Bake", GUILayout.Height(32)))
                {
                    StartBake();
                }
            }

            if (isBaking)
            {
                double elapsed = (DateTime.Now - bakeStartTime).TotalSeconds;
                EditorGUILayout.HelpBox(
                    $"Blender is running in the background ({elapsed:0.0}s elapsed). Log updates below as it works.",
                    MessageType.Info);
                if (GUILayout.Button("Cancel"))
                {
                    CancelBake();
                }
            }
            else if (!CanBake())
            {
                EditorGUILayout.HelpBox(GetValidationMessage(), MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);

            string logText;
            lock (logLock) { logText = logBuilder.ToString(); }

            float labelWidth = Mathf.Max(position.width - 40f, 50f);
            float labelHeight = Mathf.Max(EditorStyles.textArea.CalcHeight(new GUIContent(logText), labelWidth), 20f);

            if (scrollLogToBottom)
            {
                logScroll.y = labelHeight; // scroll view clamps this to its actual max automatically
                scrollLogToBottom = false;
            }

            // Scroll view fills whatever vertical space is left in the window.
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.SelectableLabel(logText, EditorStyles.textArea,
                GUILayout.Height(labelHeight), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear Log"))
            {
                lock (logLock) { logBuilder.Clear(); }
                logScroll = Vector2.zero;
            }
        }

        private bool CanBake()
        {
            var uvs = CurrentUVs;
            return File.Exists(blenderExePath)
                && fbxAsset != null
                && textureAsset != null
                && File.Exists(GetScriptPath())
                && materialIndex >= 0 && materialIndex < availableMaterials.Length
                && sourceUVIndex >= 0 && sourceUVIndex < uvs.Length
                && destUVIndex >= 0 && destUVIndex < uvs.Length
                && sourceUVIndex != destUVIndex;
        }

        private string GetValidationMessage()
        {
            if (!File.Exists(blenderExePath)) return "Set a valid path to the Blender executable.";
            if (fbxAsset == null) return "Select an FBX asset.";
            if (textureAsset == null) return "Select a source texture asset.";
            if (!File.Exists(GetScriptPath())) return "bake_uv.py must sit next to this .cs file.";
            if (availableMaterials.Length == 0) return "This FBX has no materials to select.";
            if (materialIndex < 0) return "Select a Material.";
            if (CurrentUVs.Length == 0) return "No UV maps found for this material.";
            if (sourceUVIndex < 0) return "Select a From UV.";
            if (destUVIndex < 0) return "Select a To UV.";
            if (sourceUVIndex == destUVIndex) return "From UV and To UV must be different.";
            return "";
        }

        private void DrawFbxField()
        {
            Rect fieldRect = EditorGUILayout.GetControlRect();
            int controlID = GUIUtility.GetControlID(FocusType.Passive, fieldRect);
            Event evt = Event.current;

            if (evt.type == EventType.MouseDown && evt.button == 0 && fieldRect.Contains(evt.mousePosition))
            {
                fbxPickerControlID = controlID;
                EditorGUIUtility.ShowObjectPicker<GameObject>(fbxAsset as GameObject, false, "glob:\"*.fbx\"", controlID);
                evt.Use();
            }
            else
            {
                var pickedFbxAsset = EditorGUI.ObjectField(fieldRect, "Model", fbxAsset, typeof(GameObject), false);
                if (pickedFbxAsset != fbxAsset)
                {
                    SetFbxAsset(pickedFbxAsset);
                }
            }

            if (evt.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == fbxPickerControlID)
            {
                SetFbxAsset(EditorGUIUtility.GetObjectPickerObject());
                Repaint();
            }
        }

        private void SetFbxAsset(UnityEngine.Object candidate)
        {
            if (candidate == null)
            {
                fbxAsset = null;
                return;
            }

            string path = AssetDatabase.GetAssetPath(candidate);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                AppendLogMainThread($"'{candidate.name}' is not an FBX file - ignoring selection.");
                return;
            }

            fbxAsset = candidate;
        }

        private string GetFbxPath()
        {
            if (fbxAsset == null) return "";
            string assetPath = AssetDatabase.GetAssetPath(fbxAsset);
            return string.IsNullOrEmpty(assetPath) ? "" : Path.GetFullPath(assetPath);
        }

        private string GetTexturePath()
        {
            if (textureAsset == null) return "";
            string assetPath = AssetDatabase.GetAssetPath(textureAsset);
            return string.IsNullOrEmpty(assetPath) ? "" : Path.GetFullPath(assetPath);
        }

        private string GetOutputPath()
        {
            string texturePath = GetTexturePath();
            var uvs = CurrentUVs;
            if (string.IsNullOrEmpty(texturePath) || fbxAsset == null) return "";
            if (destUVIndex < 0 || destUVIndex >= uvs.Length) return "";

            string fbxName = SanitizeForFilename(fbxAsset.name);
            string destUvName = SanitizeForFilename(uvs[destUVIndex]);

            string dir = Path.GetDirectoryName(texturePath);
            string name = Path.GetFileNameWithoutExtension(texturePath);
            string ext = Path.GetExtension(texturePath);
            return Path.Combine(dir ?? "", $"{name}_{fbxName}_{destUvName}{ext}");
        }

        private static string SanitizeForFilename(string s)
        {
            return string.IsNullOrEmpty(s) ? s : s.Replace(' ', '_');
        }

        private string GetScriptPath()
        {
            var monoScript = MonoScript.FromScriptableObject(this);
            string thisAssetPath = AssetDatabase.GetAssetPath(monoScript);
            string dir = Path.GetDirectoryName(thisAssetPath);
            string fullDir = Path.GetFullPath(dir ?? "");
            return Path.Combine(fullDir, ScriptFileName);
        }

        private void MaybeAutoFetch()
        {
            string fbxPath = GetFbxPath();
            if (fbxAsset != null && File.Exists(fbxPath))
            {
                if (fbxPath != lastFetchedFbx)
                {
                    FetchMaterialsAndUVs(fbxPath);
                }
            }
            else if (availableMaterials.Length > 0)
            {
                ClearAllState();
            }
        }

        private void RefreshAll()
        {
            string fbxPath = GetFbxPath();
            if (fbxAsset == null || !File.Exists(fbxPath))
            {
                AppendLogMainThread("Select an FBX first.");
                return;
            }

            string previousMaterial = SelectedMaterialName;
            string previousSourceUV = (sourceUVIndex >= 0 && sourceUVIndex < CurrentUVs.Length) ? CurrentUVs[sourceUVIndex] : null;
            string previousDestUV = (destUVIndex >= 0 && destUVIndex < CurrentUVs.Length) ? CurrentUVs[destUVIndex] : null;

            FetchMaterialsAndUVs(fbxPath);

            int matIdx = Array.IndexOf(availableMaterials, previousMaterial);
            if (!string.IsNullOrEmpty(previousMaterial) && matIdx >= 0)
            {
                materialIndex = matIdx;
                var uvs = CurrentUVs;

                if (!string.IsNullOrEmpty(previousSourceUV))
                {
                    int srcIdx = Array.IndexOf(uvs, previousSourceUV);
                    if (srcIdx >= 0) sourceUVIndex = srcIdx;
                }
                if (!string.IsNullOrEmpty(previousDestUV))
                {
                    int dstIdx = Array.IndexOf(uvs, previousDestUV);
                    if (dstIdx >= 0) destUVIndex = dstIdx;
                }
            }
        }

        private void ClearAllState()
        {
            availableMaterials = Array.Empty<string>();
            uvMapsByMaterial.Clear();
            lastFetchedFbx = "";
            materialIndex = -1;
            sourceUVIndex = -1;
            destUVIndex = -1;
        }

        private void FetchMaterialsAndUVs(string fbxPath)
        {
            lastFetchedFbx = fbxPath;
            materialIndex = -1;
            sourceUVIndex = -1;
            destUVIndex = -1;
            availableMaterials = Array.Empty<string>();
            uvMapsByMaterial.Clear();

            if (!File.Exists(blenderExePath) || !File.Exists(GetScriptPath()))
            {
                AppendLogMainThread("Can't query Blender - set a valid Blender executable path first.");
                return;
            }

            string scriptPath = GetScriptPath();
            string args = $"--background --factory-startup --python \"{scriptPath}\" -- --list-all \"{fbxPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = blenderExePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (var proc = Process.Start(psi))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(30000);

                    availableMaterials = ParseMarkerList(stdout, "MATLIST_START", "MATLIST_END");
                    foreach (string mat in availableMaterials)
                    {
                        uvMapsByMaterial[mat] = ParseMarkerList(stdout, $"UVLIST_START:{mat}", $"UVLIST_END:{mat}");
                    }

                    if (availableMaterials.Length == 0)
                    {
                        AppendLogMainThread("No materials found on this FBX. Blender output:");
                        AppendLogMainThread(stdout);
                        if (!string.IsNullOrEmpty(stderr)) AppendLogMainThread("[stderr] " + stderr);
                    }
                    else
                    {
                        AppendLogMainThread($"Materials: {string.Join(", ", availableMaterials)}");
                        foreach (string mat in availableMaterials)
                        {
                            var uvs = uvMapsByMaterial[mat];
                            AppendLogMainThread(uvs.Length > 0
                                ? $"  '{mat}': {string.Join(", ", uvs)}"
                                : $"  '{mat}': no UV maps found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLogMainThread("ERROR fetching materials/UVs: " + ex.Message);
                availableMaterials = Array.Empty<string>();
                uvMapsByMaterial.Clear();
            }

            if (availableMaterials.Length > 0)
            {
                materialIndex = 0;
                var uvs = CurrentUVs;
                sourceUVIndex = uvs.Length > 0 ? 0 : -1;
                destUVIndex = uvs.Length > 1 ? uvs.Length - 1 : -1;
            }

            Repaint();
        }

        private void DrawMaterialPopup()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Material", GUILayout.Width(EditorGUIUtility.labelWidth));

            if (availableMaterials.Length == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Toolbar(-1, new[] { "(none)" });
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            int newIndex = GUILayout.Toolbar(materialIndex, availableMaterials);
            EditorGUILayout.EndHorizontal();

            if (newIndex != materialIndex)
            {
                SelectMaterial(newIndex);
            }
        }

        private void SelectMaterial(int newIndex)
        {
            string previousSourceUV = (sourceUVIndex >= 0 && sourceUVIndex < CurrentUVs.Length) ? CurrentUVs[sourceUVIndex] : null;
            string previousDestUV = (destUVIndex >= 0 && destUVIndex < CurrentUVs.Length) ? CurrentUVs[destUVIndex] : null;

            materialIndex = newIndex;

            var uvs = CurrentUVs;
            sourceUVIndex = !string.IsNullOrEmpty(previousSourceUV) ? Array.IndexOf(uvs, previousSourceUV) : -1;
            destUVIndex = !string.IsNullOrEmpty(previousDestUV) ? Array.IndexOf(uvs, previousDestUV) : -1;
        }

        private void DrawUVPopup(string label, ref int index)
        {
            var uvs = CurrentUVs;
            if (uvs.Length == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Popup(label, 0, new[] { "" });
                }
                return;
            }

            index = EditorGUILayout.Popup(label, index, uvs);
        }

        private static string[] ParseMarkerList(string stdout, string startMarker, string endMarker)
        {
            var lines = stdout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new List<string>();
            bool inList = false;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed == startMarker) { inList = true; continue; }
                if (trimmed == endMarker) { inList = false; continue; }
                if (inList && trimmed.Length > 0) result.Add(trimmed);
            }
            return result.ToArray();
        }

        // Auto-detects the Blender executable across platforms.
        private static string AutoDetectBlenderPath()
        {
#if UNITY_EDITOR_WIN
            string[] roots =
            {
                @"C:\Program Files\Blender Foundation",
                @"C:\Program Files (x86)\Blender Foundation"
            };

            string best = null;
            Version bestVersion = null;
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                string[] subdirs;
                try { subdirs = Directory.GetDirectories(root); }
                catch { continue; }

                foreach (string dir in subdirs)
                {
                    string exe = Path.Combine(dir, "blender.exe");
                    if (!File.Exists(exe)) continue;

                    var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(dir), @"(\d+(?:\.\d+)+)");
                    Version v = (match.Success && Version.TryParse(match.Groups[1].Value, out var parsed))
                        ? parsed : new Version(0, 0);

                    if (bestVersion == null || v > bestVersion)
                    {
                        bestVersion = v;
                        best = exe;
                    }
                }
            }
            if (best != null) return best;
#elif UNITY_EDITOR_OSX
            string macPath = "/Applications/Blender.app/Contents/MacOS/Blender";
            if (File.Exists(macPath)) return macPath;
#elif UNITY_EDITOR_LINUX
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] linuxCandidates =
            {
                "/usr/bin/blender",
                "/usr/local/bin/blender",
                "/snap/bin/blender",
                Path.Combine(home, ".local/share/Steam/steamapps/common/Blender/blender"),
                Path.Combine(home, ".steam/steam/steamapps/common/Blender/blender"),
                Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Blender/blender")
            };

            foreach (string candidate in linuxCandidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
#endif
            string exeName = Application.platform == RuntimePlatform.WindowsEditor ? "blender.exe" : "blender";
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // malformed PATH entry, skip
                }
            }

            return null;
        }

        private static string GetBlenderFilePickerExtension()
        {
#if UNITY_EDITOR_WIN
            return "exe";
#else
            // Linux/macOS Blender executables normally have no file extension.
            return "";
#endif
        }

        // Blender is a plain filesystem path rather than a Unity project asset.
        private void DrawPathField(string label, ref string path, string extension, Action onChanged = null)
        {
            EditorGUILayout.BeginHorizontal();
            string newPath = EditorGUILayout.TextField(label, path);
            bool changed = newPath != path;
            path = newPath;
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string startDir = (!string.IsNullOrEmpty(path) && Directory.Exists(Path.GetDirectoryName(path)))
                    ? Path.GetDirectoryName(path) : "";
                string result = EditorUtility.OpenFilePanel(label, startDir, extension);
                if (!string.IsNullOrEmpty(result))
                {
                    path = result;
                    changed = true;
                }
            }
            EditorGUILayout.EndHorizontal();
            if (changed) onChanged?.Invoke();
        }

        private void StartBake()
        {
            lock (logLock) { logBuilder.Clear(); }
            string fbxPath = GetFbxPath();
            string texturePath = GetTexturePath();
            string outputPath = GetOutputPath();
            string materialName = SelectedMaterialName;
            var uvs = CurrentUVs;
            string sourceUV = uvs[sourceUVIndex];
            string destUV = uvs[destUVIndex];
            string deviceArg = DeviceModeLabels[deviceModeIndex].ToUpperInvariant(); // AUTO / GPU / CPU

            AppendLogMainThread($"Starting bake: material='{materialName}'  {sourceUV} -> {destUV}");
            AppendLogMainThread($"FBX: {fbxPath}");
            AppendLogMainThread($"Source texture: {texturePath}");
            AppendLogMainThread($"Output: {outputPath}");
            AppendLogMainThread($"Device mode: {deviceArg}");

            Texture2D capturedSourceTexture = textureAsset;

            string scriptPath = GetScriptPath();
            string args =
                $"--background --factory-startup --python \"{scriptPath}\" -- " +
                $"\"{fbxPath}\" \"{materialName}\" \"{sourceUV}\" \"{destUV}\" \"{texturePath}\" \"{outputPath}\" {marginPx} {deviceArg}";

            var psi = new ProcessStartInfo
            {
                FileName = blenderExePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            bakeStartTime = DateTime.Now;
            bakeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            bakeProcess.OutputDataReceived += (s, e) => { if (e.Data != null) QueueLog(e.Data); };
            bakeProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) QueueLog("[stderr] " + e.Data); };
            bakeProcess.Exited += (s, e) =>
            {
                int code = -1;
                try { code = bakeProcess.ExitCode; } catch { /* process may already be disposed */ }
                QueueLog(code == 0 ? "Bake finished successfully." : $"Blender exited with code {code}.");
                isBaking = false;

                if (code == 0)
                {
                    EditorApplication.delayCall += () => PostProcessOutputTexture(outputPath, capturedSourceTexture);
                }
            };

            try
            {
                bakeProcess.Start();
                bakeProcess.BeginOutputReadLine();
                bakeProcess.BeginErrorReadLine();
                isBaking = true;
                EditorApplication.update += PollRepaint;
            }
            catch (Exception ex)
            {
                AppendLogMainThread("ERROR starting Blender: " + ex.Message);
            }
        }

        private void PostProcessOutputTexture(string outputPathAbsolute, Texture2D sourceTexture)
        {
            try
            {
                string relOutput = ToProjectRelativePath(outputPathAbsolute);
                if (string.IsNullOrEmpty(relOutput))
                {
                    AppendLogMainThread("Output file is outside this Unity project's Assets folder - " +
                                         "skipping import settings copy.");
                    return;
                }

                AssetDatabase.ImportAsset(relOutput, ImportAssetOptions.ForceUpdate);

                if (sourceTexture == null)
                {
                    AppendLogMainThread("Source texture reference was lost - skipping import settings copy.");
                    return;
                }

                string sourceAssetPath = AssetDatabase.GetAssetPath(sourceTexture);
                var srcImporter = AssetImporter.GetAtPath(sourceAssetPath) as TextureImporter;
                var dstImporter = AssetImporter.GetAtPath(relOutput) as TextureImporter;

                if (srcImporter == null || dstImporter == null)
                {
                    AppendLogMainThread("Could not access texture importers - output was saved but " +
                                         "import settings weren't copied.");
                    return;
                }

                EditorUtility.CopySerialized(srcImporter, dstImporter);
                dstImporter.SaveAndReimport();

                AppendLogMainThread($"Copied import settings from '{sourceAssetPath}' to '{relOutput}'.");
            }
            catch (Exception ex)
            {
                AppendLogMainThread("ERROR copying texture import settings: " + ex.Message);
            }
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string fullDataPath = Path.GetFullPath(Application.dataPath);
            string fullAbs = Path.GetFullPath(absolutePath);
            if (!fullAbs.StartsWith(fullDataPath, StringComparison.OrdinalIgnoreCase)) return null;
            string rel = "Assets" + fullAbs.Substring(fullDataPath.Length);
            return rel.Replace('\\', '/');
        }

        private void CancelBake()
        {
            try
            {
                if (bakeProcess != null && !bakeProcess.HasExited)
                {
                    bakeProcess.Kill();
                    QueueLog("Bake cancelled by user.");
                }
            }
            catch (Exception ex)
            {
                QueueLog("Error cancelling: " + ex.Message);
            }
            isBaking = false;
        }

        private void QueueLog(string line)
        {
            lock (logLock) { logBuilder.AppendLine(line); }
            scrollLogToBottom = true;
            EditorApplication.delayCall += Repaint;
        }

        private void AppendLogMainThread(string line)
        {
            lock (logLock) { logBuilder.AppendLine(line); }
            scrollLogToBottom = true;
            Repaint();
        }

        private void PollRepaint()
        {
            if (!isBaking)
            {
                EditorApplication.update -= PollRepaint;
            }
            Repaint();
        }
    }
}
