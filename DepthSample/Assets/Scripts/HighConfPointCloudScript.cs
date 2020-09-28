using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class HighConfPointCloudScript : MonoBehaviour
{
    [SerializeField]
    ARCameraManager m_CameraManager;
    [SerializeField]
    AROcclusionManager m_OcclusionManager;
    [SerializeField]
    RawImage m_cameraView;
    [SerializeField]
    RawImage m_grayDepthView;
    [SerializeField]
    RawImage m_confidenceView;
    [SerializeField]
    Visualizer m_visualizer;
    


    [SerializeField]
    float near;
    [SerializeField]
    float far;

    Texture2D m_CameraTexture;
    Texture2D m_DepthTexture_Float;
    Texture2D m_DepthTexture_BGRA;
    Texture2D m_DepthConfidenceTexture_R8;
    Texture2D m_DepthConfidenceTexture_RGBA;

    Vector3[] vertices = null;
    Color[] colors=null;
    
    float cx, cy, fx, fy;
    bool isScanning = true;

    void OnEnable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived += OnCameraFrameReceived;
        }
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    unsafe void UpdateCameraImage()
    {

        // Attempt to get the latest camera image. If this method succeeds,
        // it acquires a native resource that must be disposed (see below).
        if (!m_CameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        using (image)
        {

            // Choose an RGBA format.
            // See XRCpuImage.FormatSupported for a complete list of supported formats.
            var format = TextureFormat.RGBA32;

            //Initialize m_CameraTexture if it's null or size of image is changed.
            if (m_CameraTexture == null || m_CameraTexture.width != image.width || m_CameraTexture.height != image.height)
            {
                m_CameraTexture = new Texture2D(image.width, image.height, format, false);
            }

            // Convert the image to format, flipping the image across the Y axis.
            // We can also get a sub rectangle, but we'll get the full image here.
            var conversionParams = new XRCpuImage.ConversionParams(image, format, XRCpuImage.Transformation.MirrorY);

            // Texture2D allows us write directly to the raw texture data
            // This allows us to do the conversion in-place without making any copies.
            var rawTextureData = m_CameraTexture.GetRawTextureData<byte>();

            //Convert XRCpuImage into RGBA32
            image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);


            // Apply the updated texture data to our texture
            m_CameraTexture.Apply();

            // Set the RawImage's texture so we can visualize it.
            m_cameraView.texture = m_CameraTexture;
        }
    }

    void UpdateEnvironmentDepthImage()
    {
        // Attempt to get the latest environment depth image. If this method succeeds,
        // it acquires a native resource that must be disposed (see below).
        if (!m_OcclusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage image))
        {
            return;
        }

        using (image)
        {
            // If the texture hasn't yet been created, or if its dimensions have changed, (re)create the texture.
            // Note: Although texture dimensions do not normally change frame-to-frame, they can change in response to
            //    a change in the camera resolution (for camera images) or changes to the quality of the human depth
            //    and human stencil buffers.
            if (m_DepthTexture_Float == null || m_DepthTexture_Float.width != image.width || m_DepthTexture_Float.height != image.height)
            {
                m_DepthTexture_Float = new Texture2D(image.width, image.height, image.format.AsTextureFormat(), false);
            }
            if (m_DepthTexture_BGRA == null || m_DepthTexture_BGRA.width != image.width || m_DepthTexture_BGRA.height != image.height)
            {
                m_DepthTexture_BGRA = new Texture2D(image.width, image.height, TextureFormat.BGRA32, false);

            }

            //Acquire Depth Image (RFloat format). Depth pixels are stored with meter unit.
            UpdateRawImage(m_DepthTexture_Float, image);
        
            //Convert RFloat into Grayscale Image between near and far clip area.
            ConvertFloatToGrayScale(m_DepthTexture_Float, m_DepthTexture_BGRA);
            //Visualize near~far depth.
            m_grayDepthView.texture = m_DepthTexture_BGRA;

        }
        
    }

    void UpdateEnvironmentConfidenceImage()
    {

        // Attempt to get the latest environment depth image. If this method succeeds,
        // it acquires a native resource that must be disposed (see below).
        if (!m_OcclusionManager.TryAcquireEnvironmentDepthConfidenceCpuImage(out XRCpuImage image))
        {
            return;
        }

        using (image)
        {
            if (m_DepthConfidenceTexture_R8 == null || m_DepthConfidenceTexture_R8.width != image.width || m_DepthConfidenceTexture_R8.height != image.height)
            {
                m_DepthConfidenceTexture_R8 = new Texture2D(image.width, image.height, image.format.AsTextureFormat(), false);
                //print(image.format.AsTextureFormat());
            }
            if (m_DepthConfidenceTexture_RGBA == null || m_DepthConfidenceTexture_RGBA.width != image.width || m_DepthConfidenceTexture_RGBA.height != image.height)
            {
                m_DepthConfidenceTexture_RGBA = new Texture2D(image.width, image.height, TextureFormat.BGRA32, false);
                
            }
            UpdateRawImage(m_DepthConfidenceTexture_R8, image);

            ConvertR8ToConfidenceMap(m_DepthConfidenceTexture_R8, m_DepthConfidenceTexture_RGBA);


            m_confidenceView.texture = m_DepthConfidenceTexture_RGBA;
        }

    }

    void UpdateRawImage(Texture2D texture, XRCpuImage cpuImage)
    {

        // For display, we need to mirror about the vertical access.
        var conversionParams = new XRCpuImage.ConversionParams(cpuImage, cpuImage.format.AsTextureFormat(), XRCpuImage.Transformation.MirrorY);

        // Get the Texture2D's underlying pixel buffer.
        var rawTextureData = texture.GetRawTextureData<byte>();

        // Make sure the destination buffer is large enough to hold the converted data (they should be the same size)
        Debug.Assert(rawTextureData.Length == cpuImage.GetConvertedDataSize(conversionParams.outputDimensions, conversionParams.outputFormat),
            "The Texture2D is not the same size as the converted data.");

        // Perform the conversion.
        cpuImage.Convert(conversionParams, rawTextureData);

        // "Apply" the new pixel data to the Texture2D.
        texture.Apply();
    }

    void ConvertFloatToGrayScale(Texture2D txFloat, Texture2D txGray)
    {

        //Conversion of grayscale from near to far value
        int length = txGray.width * txGray.height;
        Color[] depthPixels = txFloat.GetPixels();
        Color[] colorPixels = txGray.GetPixels();

        for (int index = 0; index < length; index++)
        {

            var value = (depthPixels[index].r - near) / (far - near);

            colorPixels[index].r = value;
            colorPixels[index].g = value;
            colorPixels[index].b = value;
            colorPixels[index].a = 1;
        }
        txGray.SetPixels(colorPixels);
        txGray.Apply();
    }

    void ConvertR8ToConfidenceMap(Texture2D txR8, Texture2D txRGBA) {
        Color32[] r8 = txR8.GetPixels32();
        Color32[] rgba = txRGBA.GetPixels32();
        for (int i = 0; i < r8.Length; i++)
        {
            switch (r8[i].r)
            {
                case 0:
                    rgba[i].r = 255;
                    rgba[i].g = 0;
                    rgba[i].b = 0;
                    rgba[i].a = 255;
                    break;
                case 1:
                    rgba[i].r = 0;
                    rgba[i].g = 255;
                    rgba[i].b = 0;
                    rgba[i].a = 255;
                    break;
                case 2:
                    rgba[i].r = 0;
                    rgba[i].g = 0;
                    rgba[i].b = 255;
                    rgba[i].a = 255;
                    break;
            }
        }
        txRGBA.SetPixels32(rgba);
        txRGBA.Apply();
    }


    void ReprojectPointCloud()
    {
        print("Depth:" + m_DepthTexture_Float.width + "," + m_DepthTexture_Float.height);
        print("Color:" + m_CameraTexture.width + "," + m_CameraTexture.height);
        int width_depth = m_DepthTexture_Float.width;
        int height_depth = m_DepthTexture_Float.height;
        int width_camera = m_CameraTexture.width;

        if(vertices==null || colors == null)
        {
            vertices = new Vector3[width_depth * height_depth];
            colors = new Color[width_depth * height_depth];
          
            XRCameraIntrinsics intrinsic;
            m_CameraManager.TryGetIntrinsics(out intrinsic);
            print("intrinsics:" + intrinsic);

            float ratio = (float) width_depth / (float)width_camera;
            fx = intrinsic.focalLength.x * ratio;
            fy = intrinsic.focalLength.y * ratio;

            cx = intrinsic.principalPoint.x * ratio;
            cy = intrinsic.principalPoint.y * ratio;

        }

        Color[] depthPixels = m_DepthTexture_Float.GetPixels();
        Color32[] confidenceMap = m_DepthConfidenceTexture_R8.GetPixels32(); 
        int index_dst ;
        float depth;
        for(int depth_y = 0; depth_y < height_depth; depth_y++)
        {
            index_dst = depth_y * width_depth;
            for(int depth_x = 0; depth_x < width_depth; depth_x++)
            {
                colors[index_dst] = m_CameraTexture.GetPixelBilinear((float)depth_x/(width_depth), (float)depth_y / (height_depth));

                depth = depthPixels[index_dst].r;
                if (depth > near && depth < far && confidenceMap[index_dst].r==2)
                {
                    vertices[index_dst].z = depth;
                    vertices[index_dst].x = -depth * (depth_x - cx) / fx;
                    vertices[index_dst].y = -depth * (depth_y - cy) / fy;
                }
                else
                {
                    vertices[index_dst].z = -999;
                    vertices[index_dst].x = 0;
                    vertices[index_dst].y = 0;
                }
                index_dst++;
            }
        }


        m_visualizer.UpdateMeshInfo(vertices, colors);


    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        UpdateCameraImage();
        UpdateEnvironmentDepthImage();
        UpdateEnvironmentConfidenceImage();
        if (isScanning)
        {
            ReprojectPointCloud();
        }
    }


    public void SwitchScanMode(bool flg)
    {
        isScanning = flg;
        if (flg)
        {
            m_visualizer.transform.parent = m_CameraManager.transform;
            m_visualizer.transform.localPosition = Vector3.zero;
            m_visualizer.transform.localRotation = Quaternion.identity;
        }
        else
        {
            m_visualizer.transform.parent = null;
        }
    }

    private void Start()
    {
        m_visualizer.transform.parent = m_CameraManager.transform;
    }

}
