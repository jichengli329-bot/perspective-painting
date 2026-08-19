#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// The stable Unity-side contract for authored DCC assets. Files outside
    /// SourceModels are deliberately untouched so legacy prototype content
    /// cannot be changed by an importer upgrade.
    /// </summary>
    public sealed class ProductionModelImportPolicy : AssetPostprocessor
    {
        public const string SourceRoot = "Assets/Art/SourceModels/";
        private static readonly Regex ProductionName = new Regex(
            @"^(HERO|PROP|ENV)_[A-Z][A-Za-z0-9]*_v[0-9]{3}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(SourceRoot, StringComparison.Ordinal))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
        }

        [MenuItem("Tools/PerspectivePuzzle/Production Art/Validate Source Models")]
        public static void ValidateAllMenu()
        {
            IReadOnlyList<string> issues = ValidateAll();
            if (issues.Count == 0)
            {
                Debug.Log("[ProductionModelImportPolicy] SourceModels validation passed.");
                return;
            }

            throw new InvalidOperationException(
                "Production model validation failed:\n- " + string.Join("\n- ", issues));
        }

        public static IReadOnlyList<string> ValidateAll()
        {
            var issues = new List<string>();
            if (!AssetDatabase.IsValidFolder(SourceRoot.TrimEnd('/')))
                return issues;

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { SourceRoot.TrimEnd('/') });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string stem = Path.GetFileNameWithoutExtension(path);
                if (!ProductionName.IsMatch(stem))
                    issues.Add(path + " must use ROLE_Name_v### naming.");

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    issues.Add(path + " has no ModelImporter.");
                    continue;
                }

                if (Mathf.Abs(importer.globalScale - 1f) > 0.0001f)
                    issues.Add(path + " has a non-unit global scale.");
                if (importer.importCameras || importer.importLights)
                    issues.Add(path + " imports DCC cameras or lights.");
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
                    issues.Add(path + " imports embedded materials.");
            }

            return issues;
        }
    }
}
#endif
