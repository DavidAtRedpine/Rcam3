using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ApplyMultipleBoxesShader : MonoBehaviour
{
    public RawImage sourceColorImage;
    public RawImage sourceDepthImage;
    
    public RenderTexture targetColorRenderTexture;  // Output render texture
    public RenderTexture targetDepthRenderTexture;  // Output render texture
    public Material colorMaterial;  // Material with the custom shader
    public Material depthMaterial;  // Material with the custom shader

    [System.Serializable]
    public struct Box
    {
        public Vector2 min; // Bottom-left corner (UV coordinates)
        public Vector2 max; // Top-right corner (UV coordinates)
    }

    public List<Box> boxes;

    //public Box[] boxes; // Array of boxes (defined in UV space)

    void Start()
    {
        
        
    }

    void Update()
    {
        ApplyShaderWithBoxes();
    }

    public void ApplyShaderWithBoxes()
    {
        var sourceColorRenderTexture = sourceColorImage.mainTexture;
        var sourceDepthRenderTexture = sourceDepthImage.mainTexture;
        
        if (sourceColorRenderTexture == null || targetColorRenderTexture == null || sourceDepthRenderTexture == null || targetDepthRenderTexture == null || depthMaterial == null)
        {
            Debug.LogError("Please assign the sourceRenderTexture, targetRenderTexture, and redOutsideMultipleBoxesMaterial.");
            return;
        }

        if (boxes.Count > 16)
        {
            Debug.LogError("The shader supports a maximum of 16 boxes.");
            return;
        }

        // Prepare the box data
        Vector4[] boxData = new Vector4[boxes.Count];
        for (int i = 0; i < boxes.Count; i++)
        {
            boxData[i] = new Vector4(boxes[i].min.x, boxes[i].min.y, boxes[i].max.x, boxes[i].max.y);
        }

        colorMaterial.SetInt("_RED", 0);
        depthMaterial.SetInt("_RED", 1);
        
        // Set the box data and count in the material
        if(boxData != null && boxData.Length > 0)
        {
            depthMaterial.SetInt("_BoxCount", boxes.Count);
            depthMaterial.SetVectorArray("_BoxMinMax", boxData);
            colorMaterial.SetInt("_BoxCount", boxes.Count);
            colorMaterial.SetVectorArray("_BoxMinMax", boxData);
        }
        else
        {
            depthMaterial.SetInt("_BoxCount", 0);
            colorMaterial.SetInt("_BoxCount", 0);
        }
        

        // Apply the shader using Graphics.Blit
        Graphics.Blit(sourceColorRenderTexture, targetColorRenderTexture, colorMaterial);
        Graphics.Blit(sourceDepthRenderTexture, targetDepthRenderTexture, depthMaterial);
    }
}
