using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace WowUnity
{
    class URPMaterialProcessor
    {
        public const string LIT_SHADER = "Universal Render Pipeline/Simple Lit";
        public const string UNLIT_SHADER = "Universal Render Pipeline/Unlit";
        public const string EFFECT_SHADER = "Universal Render Pipeline/Particles/Unlit";

        public static Material ConfigureMaterial(MaterialDescription description, Material material, string modelImportPath, M2Utility.M2 metadata)
        {
            M2Utility.Material materialData = M2Utility.GetMaterialData(material.name, metadata);
            Color materialColor = Color.white;
            if (metadata != null && metadata.colors.Count > 0)
            {
                materialColor = MaterialUtility.ProcessMaterialColors(material, metadata);
            }
            
            material.shader = Shader.Find(LIT_SHADER);
            material.SetColor("_BaseColor", materialColor);

            // Read a texture property from the material description.
            TexturePropertyDescription textureProperty;
            if (description.TryGetProperty("DiffuseColor", out textureProperty) && textureProperty.texture != null)
            {
                // Assign the texture to the material.
                material.SetTexture("_MainTex", textureProperty.texture);
            }
                
            ProcessFlagsForMaterial(material, materialData);
            
            return material;
        }

        public static void ProcessFlagsForMaterial(Material material, M2Utility.Material data)
        {
            //Flags first
            if ((data.flags & (short)MaterialUtility.MaterialFlags.Unlit) != (short)MaterialUtility.MaterialFlags.None)
            {
                material.shader = Shader.Find(UNLIT_SHADER);
            }

            if ((data.flags & (short)MaterialUtility.MaterialFlags.TwoSided) != (short)MaterialUtility.MaterialFlags.None)
            {
                material.doubleSidedGI = true;
                material.SetFloat("_Cull", 0);
            }

            //Now blend modes
            if (data.blendingMode == (short)MaterialUtility.BlendModes.AlphaKey)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_AlphaClip", 1);
            }

            if (data.blendingMode == (short)MaterialUtility.BlendModes.Alpha)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Blend", 0);
                material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
            }

            if (data.blendingMode == (short)MaterialUtility.BlendModes.Add)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetFloat("_Cutoff", 0);
                material.SetFloat("_Blend", 1);
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 1);
                material.SetFloat("_ZWrite", 0);
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
        }

        public static void ExtractMaterialFromAsset(Material material)
        {
            string assetPath = AssetDatabase.GetAssetPath(material);
            string newMaterialPath = "Assets/Materials/" + material.name + ".mat";
            Material newMaterialAsset;

            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
            }
            
            if (!File.Exists(newMaterialPath))
            {
                newMaterialAsset = new Material(material);
                AssetDatabase.CreateAsset(newMaterialAsset, newMaterialPath);
            }
            else
            {
                newMaterialAsset = AssetDatabase.LoadAssetAtPath<Material>(newMaterialPath);
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), newMaterialAsset);

            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
