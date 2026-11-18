using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Menu Tools > Color Mixing > Import Client Requests.
// Mengganti SELURUH isi ClientRequestDatabase dengan isi Assets/Data/ClientRequests.tsv (bisa di-Undo).
// Kolom TSV (baris 1 = header): requestId, difficulty, requestName, targetHex, description.
// Kolom setelahnya (kunci jawaban) hanya untuk referensi dan diabaikan.
public static class ClientRequestImporter
{
    private const string TsvPath = "Assets/Data/ClientRequests.tsv";
    private const string DatabasePath = "Assets/Data/ClientRequestDatabase.asset";

    private struct Row
    {
        public string id, name, description;
        public RequestDifficulty difficulty;
        public Color color;
    }

    [MenuItem("Tools/Color Mixing/Import Client Requests")]
    private static void Import()
    {
        var database = AssetDatabase.LoadAssetAtPath<ClientRequestDatabase>(DatabasePath);
        if (database == null) { Debug.LogError($"[IMPORT] Database tidak ditemukan: {DatabasePath}"); return; }
        if (!File.Exists(TsvPath)) { Debug.LogError($"[IMPORT] File tidak ditemukan: {TsvPath}"); return; }

        // Validasi semua baris dulu. Satu baris rusak = batal, database tidak disentuh.
        var rows = new List<Row>();
        var ids = new HashSet<string>();
        string[] lines = File.ReadAllLines(TsvPath);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] c = lines[i].Split('\t');
            if (c.Length < 5
                || string.IsNullOrWhiteSpace(c[0])
                || !ids.Add(c[0])
                || !Enum.TryParse(c[1], out RequestDifficulty difficulty)
                || !ColorUtility.TryParseHtmlString(c[3], out Color color))
            {
                Debug.LogError($"[IMPORT] Baris {i + 1} tidak valid (kolom kurang / id dobel / difficulty / hex salah). Import dibatalkan.\n{lines[i]}");
                return;
            }
            rows.Add(new Row { id = c[0], difficulty = difficulty, name = c[2], color = color, description = c[4] });
        }

        var so = new SerializedObject(database);
        var list = so.FindProperty("clientRequests");
        list.arraySize = rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("requestId").stringValue = rows[i].id;
            e.FindPropertyRelative("requestName").stringValue = rows[i].name;
            e.FindPropertyRelative("description").stringValue = rows[i].description;
            e.FindPropertyRelative("targetColor").colorValue = rows[i].color;
            e.FindPropertyRelative("difficulty").enumValueIndex = (int)rows[i].difficulty;
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[IMPORT] {rows.Count} request diimpor ke {DatabasePath}");
    }
}
