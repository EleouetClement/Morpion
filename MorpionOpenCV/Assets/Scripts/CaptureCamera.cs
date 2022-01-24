using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Drawing;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using System;
using System.Linq;

public class CaptureCamera : MonoBehaviour
{

    private VideoCapture webCamera = new VideoCapture(0);
    public RawImage webcamScreen;
    private Mat imageGrabbed = new Mat();
    private Mat grayImaged = new Mat();
    private const int Treshold = 600;
    Texture2D tex;

    private System.Drawing.Rectangle[] faces = new System.Drawing.Rectangle[1];


    // Start is called before the first frame update
    void Start()
    {
        webCamera.ImageGrabbed += HandleWebcamQueryFrame;
        webCamera.Start();
    }

    // Update is called once per frame
    void Update()
    {
        if (webCamera.IsOpened)
        {
            Mat imageGrabbed = new Mat();
            webCamera.Grab();
            //webCamera.Retrieve(imageGrabbed);
            //CvInvoke.Imshow("testWindow", imageGrabbed);
            DisplayFrameOnPlane();
        }
    }

    private void OnDestroy()
    {

        if (webCamera != null)
        {
            Debug.LogWarning("Camera down");
            webCamera.Stop();
            webCamera.Dispose();
        }
    }

    private void HandleWebcamQueryFrame(object sender, System.EventArgs e)
    {
        if (webCamera.IsOpened)
        {
            webCamera.Retrieve(imageGrabbed);
            //Debug.Log("camera openned");

            lock (imageGrabbed)
            {
                var original = imageGrabbed;
                CvInvoke.CvtColor(imageGrabbed, imageGrabbed, ColorConversion.Bgr2Gray);
                CvInvoke.GaussianBlur(imageGrabbed, imageGrabbed, new Size(3, 3), 1);
                ShapeDetection(imageGrabbed, original);
            }
        }

        System.Threading.Thread.Sleep(200);
        //Debug.Log(imageGrabbed.Size);
    }

    private void DisplayFrameOnPlane()
    {
        if (tex != null)
        {
            Destroy(tex);
            tex = null;
        }

        int width = (int)webcamScreen.rectTransform.rect.width;
        int height = (int)webcamScreen.rectTransform.rect.height;
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        if (imageGrabbed.IsEmpty)
        {
            Debug.LogError("Image vide");
            return;
        }

        CvInvoke.Resize(imageGrabbed, imageGrabbed, new System.Drawing.Size(width, height));
        CvInvoke.CvtColor(imageGrabbed, imageGrabbed, ColorConversion.Bgr2Rgba);
        CvInvoke.Flip(imageGrabbed, imageGrabbed, FlipType.Vertical);

        if (imageGrabbed == null)
        {
            Debug.LogError("DisplayFrameOnPlane : ImageGrabbed is null");
            return;
        }
        if (tex == null)
        {
            Debug.LogError("DisplayFrameOnPlane : tex is null");
            return;
        }
        tex.LoadRawTextureData(imageGrabbed.ToImage<Rgba, byte>().Bytes);
        tex.Apply();

        webcamScreen.texture = tex;
    }

    private void ShapeDetection(Mat imageGrabbed, Mat original)
    {
        double seuil = 180.0;
        double circleAccumulatorThreshold = 120;

        CircleF[] circles = CvInvoke.HoughCircles(imageGrabbed, HoughModes.Gradient, 2.0, 2.0, seuil, circleAccumulatorThreshold, 5);
        
        if (circles == null || circles.Length == 0)
        {
            Debug.LogWarning("No circle");
        }
        else
        {
            //Detect color
            Debug.Log("Circle!");
            ColorDetection(original, circles.FirstOrDefault().Center);
        }

    }
    private Vector3 ColorDetection(Mat imageGrabbed, PointF center)
    {
        var image = imageGrabbed.ToImage<Bgr, byte>();
        Vector3 color = new Vector3(image.Data[(int)center.X, (int)center.Y, 0],
            image.Data[(int)center.X, (int)center.Y, 1],
            image.Data[(int)center.X, (int)center.Y, 3]);
        return color;
        //for(float x = center.X-1f; x < center.X + 1f; x++ )
        //{
        //    for (float y = center.Y - 1f; y < center.Y + 1f; y++)
        //    {

        //    }
        //}
    }
}
