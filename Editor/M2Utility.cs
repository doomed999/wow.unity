﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class M2Utility
    {
        private static List<string> importedModelPathQueue = new List<string>();

        public static void QueueMetadata(string filePath)
        {
            importedModelPathQueue.Add(filePath);
        }

        public static void PostProcessImports()
        {
            if (importedModelPathQueue.Count == 0)
            {
                return;
            }

            List<string> iteratingList = new List<string>(importedModelPathQueue);

            foreach (string path in iteratingList)
            {
                M2 metadata = ReadMetadataFor(path);

                if (metadata == null)
                    continue;

                GameObject prefab = FindOrCreatePrefab(path);

                Debug.Log("Processing metadata for: " + metadata.fileName);

                try
                {

                    if (metadata.textureTransforms.Count > 0 && metadata.textureTransforms[0].translation.timestamps.Count > 0)
                    {
                        for (int i = 0; i < metadata.textureTransforms.Count; i++)
                        {
                            Debug.Log("Creating animation clips for: " + metadata.fileName);
                            Debug.Log("Creating animation clip metadata.textureTransforms[i].translation.timestamps.Count: " + metadata.textureTransforms[i].translation.timestamps.Count);
                            Debug.Log("Creating animation clip metadata.textureTransforms[i].translation.values.Count" + metadata.textureTransforms[i].translation.values.Count);
                            AnimationClip newClip = AnimationUtility.CreateAnimationClip(metadata.textureTransforms[i]);
                            AssetDatabase.CreateAsset(newClip, Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "[" + i + "]" + ".anim");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to create animation clip for: " + metadata.fileName);
                    Debug.LogError(e);
                }
            }

            //Processing done: remove all paths from the queue
            importedModelPathQueue.Clear();
        }

        public static GameObject FindOrCreatePrefab(string path)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (existingPrefab == null)
            {
                return GeneratePrefab(path);
            }

            return existingPrefab;
        }

        public static GameObject GeneratePrefab(string path)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            GameObject importedModelObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (importedModelObject == null)
            {
                Debug.LogWarning("Tried to create prefab, but could not find imported model: " + path);
                return null;
            }

            ConfigureRendererMaterials(importedModelObject);

            ModelImporter modelImporter = ModelImporter.GetAtPath(path) as ModelImporter;
            modelImporter.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.RecursiveUp);

            GameObject rootModelInstance = PrefabUtility.InstantiatePrefab(importedModelObject) as GameObject;

            //Set the object as static, and all it's child objects
            rootModelInstance.isStatic = true;
            foreach (Transform childTransform in rootModelInstance.transform)
            {
                childTransform.gameObject.isStatic = true;
            }

            GameObject newPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(rootModelInstance, prefabPath, InteractionMode.AutomatedAction);
            AssetDatabase.Refresh();
            UnityEngine.Object.DestroyImmediate(rootModelInstance);

            return newPrefab;
        }

        private static void ConfigureRendererMaterials(GameObject importedModelObject)
        {
            //Manage materials for imported models.

            //First, we need to sample all renderers that belong to the specified game object.
            Renderer[] renderers = importedModelObject.GetComponentsInChildren<Renderer>();

            //Now we will loop through all renderers present in the game object
            //and call the MaterialUtility to create the asset.
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                for (int materialIndex = 0; materialIndex < renderers[rendererIndex].sharedMaterials.Length; materialIndex++)
                {
                    //We don't need to worry about repeat materials here,
                    //because the CreateMaterialAsset already handles this case for us.
                    MaterialUtility.ExtractMaterialFromAsset(renderers[rendererIndex].sharedMaterials[materialIndex]);
                }
            }
            AssetDatabase.Refresh();
        }

        public static M2 ReadMetadataFor(string path)
        {
            string pathToMetadata = Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + ".json";

            if (!File.Exists(pathToMetadata))
            {
                return null;
            }

            var sr = new StreamReader(Application.dataPath.Replace("Assets", "") + pathToMetadata);
            var fileContents = sr.ReadToEnd();
            sr.Close();

            return JsonConvert.DeserializeObject<M2>(fileContents);
        }

        public static Material GetMaterialData(string materialName, M2 metadata)
        {
            Material data = new Material();
            data.flags = 0;
            data.blendingMode = 0;

            //I have no idea why they sometimes don't match sizes.
            // I'm guessing if there's no material entry, default is intended.
            for (int i = 0; i < metadata.textures.Count; i++)
            {
                Texture texture = metadata.textures[i];
                if (texture.mtlName != materialName)
                    continue;

                if (metadata.materials.Count <= i)
                    i = metadata.materials.Count - 1;

                data = metadata.materials[i];
                break;
            }

            return data;
        }

        [Serializable]
        public class M2
        {
            public uint fileDataID;
            public string fileName;
            public string internalName;
            public Skin skin;
            public List<Texture> textures = new List<Texture>();
            public List<short> textureTypes = new List<short>();
            public List<Material> materials = new List<Material>();
            public List<short> textureCombos = new List<short>();
            public List<ColorData> colors = new List<ColorData>();
            public List<TextureTransform> textureTransforms = new List<TextureTransform>();
            public List<uint> textureTransformsLookup = new List<uint>();
        }

        [Serializable]
        public class Skin
        {
            public List<SubMesh> subMeshes = new List<SubMesh>();
            public List<TextureUnit> textureUnits = new List<TextureUnit>();
        }

        [Serializable]
        public struct SubMesh
        {
            public bool enabled;
        }

        [Serializable]
        public struct TextureUnit
        {
            public uint skinSelectionIndex;
            public uint geosetIndex;
            public uint colorIndex;
        }

        [Serializable]
        public struct Texture
        {
            public string fileNameInternal;
            public string fileNameExternal;
            public string mtlName;
            public short flag;
            public uint fileDataID;
        }

        [Serializable]
        public struct Material
        {
            public short flags;
            public uint blendingMode;
        }

        [Serializable]
        public struct ColorData
        {
            public MultiValueAnimationInformation color;
            public SingleValueAnimationInformation alpha;
        }

        [Serializable]
        public struct TextureTransform
        {
            public MultiValueAnimationInformation translation;
            public MultiValueAnimationInformation rotation;
            public MultiValueAnimationInformation scaling;
        }

        [Serializable]
        public struct SingleValueAnimationInformation
        {
            public uint globalSeq;
            public int interpolation;
            public List<List<uint>> timestamps;
            public List<List<float>> values;
        }

        [Serializable]
        public struct MultiValueAnimationInformation
        {
            public uint globalSeq;
            public int interpolation;
            public List<List<uint>> timestamps;
            public List<List<List<float>>> values;
        }
    }
}
