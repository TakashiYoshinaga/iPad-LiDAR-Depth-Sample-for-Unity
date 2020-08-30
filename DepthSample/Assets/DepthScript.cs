using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class DepthScript : MonoBehaviour
{
    [SerializeField]
    ARCameraManager m_CameraManager;
    [SerializeField]
    AROcclusionManager m_OcclusionManager;
    [SerializeField]
    RawImage m_cameraView;
    [SerializeField]
    RawImage m_originalDepthView;
    [SerializeField]
    RawImage m_grayDepthView;


    [SerializeField]
    float near;
    [SerializeField]
    float far;

    Texture2D m_CameraTexture;
    Texture2D m_DepthTextureFloat;
    Texture2D m_DepthTextureBGRA;

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
            if (m_DepthTextureFloat == null || m_DepthTextureFloat.width != image.width || m_DepthTextureFloat.height != image.height)
            {
                m_DepthTextureFloat = new Texture2D(image.width, image.height, image.format.AsTextureFormat(), false);
            }
            if (m_DepthTextureBGRA == null || m_DepthTextureBGRA.width != image.width || m_DepthTextureBGRA.height != image.height)
            {
                m_DepthTextureBGRA = new Texture2D(image.width, image.height, TextureFormat.BGRA32, false);

            }

            //Acquire Depth Image (RFloat format). Depth pixels are stored with meter unit.
            UpdateRawImage(m_DepthTextureFloat, image);
            //Visualize 0~1m depth.
            m_originalDepthView.texture = m_DepthTextureFloat;

            //Convert RFloat into Grayscale Image between near and far clip area.
            ConvertFloatToGrayScale(m_DepthTextureFloat, m_DepthTextureBGRA);
            //Visualize near~far depth.
            m_grayDepthView.texture = m_DepthTextureBGRA;

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



    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        UpdateCameraImage();
        UpdateEnvironmentDepthImage();
    }

    // Start is called before the first frame update
    void Start()
    {

    }



}
