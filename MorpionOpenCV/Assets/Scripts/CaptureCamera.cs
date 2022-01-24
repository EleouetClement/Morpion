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
using System.Globalization;

public class CaptureCamera : MonoBehaviour
{

    private VideoCapture webCamera = new VideoCapture(0);
    public RawImage webcamScreen;
    private Mat imageGrabbed = new Mat();
    UMat cannyEdges = new UMat();
    Texture2D tex;
    public List<GameObject> gridComponents;
    private Dictionary<GameObject, bool> gridFlags;
    public GameObject trianglePrefab;
    public GameObject circlePrefab;
    private bool isCircleTurn = true;
    private bool isTriangleTurn = false;
    // Start is called before the first frame update
    void Start()
    {
        webCamera.ImageGrabbed += HandleWebcamQueryFrame;
        webCamera.Start();
        gridFlags = new Dictionary<GameObject, bool>();
        foreach (var comp in gridComponents)
        {
            gridFlags.Add(comp, false);
        }
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

    /// <summary>
    /// copy one image to an empty one
    /// </summary>
    /// <param name="img1"></param>
    /// <param name="img2"></param>
    /// <param name="offsetX"></param>
    /// <param name="offsetY"></param>
    public static void CopyToImage(Image<Bgr, byte> img1, Image<Bgr, byte> img2, int offsetX, int offsetY)
    {
        for (int i = 0; i < img1.Height; i++)
        {
            for (int j = 0; j < img1.Width; j++)
            {
                img2.Data[i + offsetY, j + offsetX, 0] = img1.Data[i, j, 0];
                img2.Data[i + offsetY, j + offsetX, 1] = img1.Data[i, j, 1];
                img2.Data[i + offsetY, j + offsetX, 2] = img1.Data[i, j, 2];
            }
        }
    }

    /// <summary>
    /// for each frame, process it to detect colors and shapes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleWebcamQueryFrame(object sender, System.EventArgs e)
    {
        imageGrabbed = new Mat();
        if (webCamera.IsOpened)
        {
            webCamera.Retrieve(imageGrabbed);
            //Debug.Log("camera openned");

            lock (imageGrabbed)
            {
                var image = imageGrabbed.ToImage<Bgr, byte>();
                var original = new Image<Bgr, byte>(image.Width, image.Height);
                var grayCopy = new Mat();
                Mat matRes = original.Mat;
                imageGrabbed.CopyTo(grayCopy);
                CopyToImage(image, original, 0, 0);
                CvInvoke.CvtColor(imageGrabbed, grayCopy, ColorConversion.Bgr2Gray);
                CvInvoke.GaussianBlur(grayCopy, grayCopy, new Size(3, 3), 1);
                CvInvoke.Imshow("imag", imageGrabbed);
                ShapeDetection(imageGrabbed, original, grayCopy);
            }
        }

        System.Threading.Thread.Sleep(50);
        //Debug.Log(imageGrabbed.Size);
    }

    /// <summary>
    /// Display the frame on the Unity image
    /// </summary>
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

    /// <summary>
    /// Detect if the shape is a circle or a cross
    /// </summary>
    /// <param name="imageGrabbed"></param>
    /// <param name="original"></param>
    private void ShapeDetection(Mat imageGrabbed, Image<Bgr, byte> original, Mat grayCopy)
    {
        double seuil = 180.0;
        double circleAccumulatorThreshold = 120;

        if (isCircleTurn)
        {
            CircleF[] circles = CvInvoke.HoughCircles(grayCopy, HoughModes.Gradient, 2.0, 2.0, seuil, circleAccumulatorThreshold, 5);
            if (circles == null || circles.Length == 0)
            {
                Debug.LogWarning("No circle detected yet");
            }
            else
            {
                //Detect color
                //Debug.Log("Circle!");
                var color = ColorDetection(original, circles.FirstOrDefault().Center);
                var matchComponent = MatchColorWithGrid(color);
                if (matchComponent != null)
                {
                    Instantiate(circlePrefab, matchComponent.transform.position, Quaternion.identity);
                    isCircleTurn = false;
                    isTriangleTurn = true;
                    Debug.Log("Cercle a joue");
                }
                else
                    Debug.LogError("No color has been matched");
            }

        }

        if (isTriangleTurn)
        {
            List<Triangle2DF> triangles = DetectTriangle(grayCopy);
            if (triangles == null || triangles.Count == 0)
            {
                Debug.LogWarning("No triangles");
            }
            else
            {
                //Debug.Log("Triangles");
                var color = ColorDetection(original, triangles[0].Centeroid);
                var matchComponent = MatchColorWithGrid(color);
                if (matchComponent != null)
                {
                    Instantiate(trianglePrefab, matchComponent.transform.position, Quaternion.identity);
                    isTriangleTurn = false;
                    isCircleTurn = true;
                    Debug.Log("Triangle a joue");
                }
                else
                    Debug.LogError("No color has been matched");
            }
        }
    }

    /// <summary>
    /// Try to detect a triangle in the Image
    /// </summary>
    /// <param name="grayCopy"></param>
    /// <param name="original"></param>
    private List<Triangle2DF> DetectTriangle(Mat grayCopy)
    {
        double cannyThreshold = 180.0;
        double cannyThresholdLinking = 120.0;
        CvInvoke.Canny(grayCopy, cannyEdges, cannyThreshold, cannyThresholdLinking);
        LineSegment2D[] lines = CvInvoke.HoughLinesP(cannyEdges, 1, Math.PI / 45.0, 20, 30, 10);
        List<Triangle2DF> triangleList = new List<Triangle2DF>();
        List<RotatedRect> boxList = new List<RotatedRect>(); //a box is a rotated rectangle
        using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
        {
            CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
            int count = contours.Size;
            for (int i = 0; i < count; i++)
            {
                using (VectorOfPoint contour = contours[i])
                using (VectorOfPoint approxContour = new VectorOfPoint())
                {
                    CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.05,
                        true);
                    if (CvInvoke.ContourArea(approxContour, false) > 250) //only consider contours with area greater than 250
                    {
                        if (approxContour.Size == 3) //The contour has 3 vertices, it is a triangle
                        {
                            Point[] pts = approxContour.ToArray();
                            triangleList.Add(new Triangle2DF(pts[0], pts[1], pts[2]));

                        }
                    }
                }
            }
            if (triangleList.Count > 0)
            {
                return triangleList;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Detect the color of the shape
    /// </summary>
    /// <param name="imageGrabbed"></param>
    /// <param name="center"></param>
    /// <returns></returns>
    private Vector3 ColorDetection(Image<Bgr, byte> imageGrabbed, PointF center)
    {
        //switch last and 1st values to get rgb and not bgr
        Vector3 color = new Vector3(imageGrabbed.Data[(int)center.X, (int)center.Y, 2],
            imageGrabbed.Data[(int)center.X, (int)center.Y, 1],
            imageGrabbed.Data[(int)center.X, (int)center.Y, 0]);
        return color;
    }
    GameObject MatchColorWithGrid(Vector3 color)
    {
        float minDistance = float.MaxValue;
        GameObject match = null;
        Vector4 labColor = RGBToLab(new Vector4(color.x, color.y, color.z, 255));
        bool pouet = IsItGray(color);
        if (!(gridComponents == null || gridComponents.Count == 0) && !pouet)
        {
            foreach (var comp in gridComponents)
            {
                var compColor = comp.GetComponent<Renderer>().material.color;
                Vector4 labCompColor = RGBToLab(new Vector4(compColor.r, compColor.g, compColor.b, compColor.a));
                float deltaE = DeltaE(labColor, labCompColor);
                var distance = Vector3.Distance(color, new Vector3(compColor.r, compColor.g, compColor.b));
                if (deltaE < minDistance)
                {
                    minDistance = deltaE;
                    match = comp;
                }
            }
            if (match != null && !gridFlags[match])
            {
                lock(gridFlags)
                {
                    gridFlags[match] = true;
                    return match;
                }             
            }
        }
        return null;
    }
    
    private bool IsItGray(Vector3 color)
    {
        //TO DO
        float xy = Mathf.Abs(color.x - color.y);
        if(xy < 25)
        {
            float xz = Mathf.Abs(color.x - color.z);
            if(xz < 25)
            {
                return true;
            }
        }
        return false;//TO CHANGE
    }


    /// <summary>
    /// Calculate the Delta-e value of to colors in 
    /// the lab space
    /// </summary>
    /// <param name="labColorA"></param>
    /// <param name="labColorB"></param>
    /// <returns></returns>
    public float DeltaE(Vector4 labColorA, Vector4 labColorB)
    {
        float deltaE = Mathf.Epsilon;
        float l = Mathf.Pow((labColorB.x - labColorA.x), 2);
        float a = Mathf.Pow((labColorB.y - labColorA.y), 2);
        float b = Mathf.Pow((labColorB.z - labColorA.z), 2);
        deltaE = Mathf.Sqrt(l + a + b);
        return deltaE;
    }

    public static Vector4 RGBToLab(Vector4 color)
    {
        float[] xyz = new float[3];
        float[] lab = new float[3];
        float[] rgb = new float[] { color[0], color[1], color[2], color[3] };

        rgb[0] = color[0] / 255.0f;
        rgb[1] = color[1] / 255.0f;
        rgb[2] = color[2] / 255.0f;

        if (rgb[0] > .04045f)
        {
            rgb[0] = (float)Math.Pow((rgb[0] + .055) / 1.055, 2.4);
        }
        else
        {
            rgb[0] = rgb[0] / 12.92f;
        }

        if (rgb[1] > .04045f)
        {
            rgb[1] = (float)Math.Pow((rgb[1] + .055) / 1.055, 2.4);
        }
        else
        {
            rgb[1] = rgb[1] / 12.92f;
        }

        if (rgb[2] > .04045f)
        {
            rgb[2] = (float)Math.Pow((rgb[2] + .055) / 1.055, 2.4);
        }
        else
        {
            rgb[2] = rgb[2] / 12.92f;
        }
        rgb[0] = rgb[0] * 100.0f;
        rgb[1] = rgb[1] * 100.0f;
        rgb[2] = rgb[2] * 100.0f;


        xyz[0] = ((rgb[0] * .412453f) + (rgb[1] * .357580f) + (rgb[2] * .180423f));
        xyz[1] = ((rgb[0] * .212671f) + (rgb[1] * .715160f) + (rgb[2] * .072169f));
        xyz[2] = ((rgb[0] * .019334f) + (rgb[1] * .119193f) + (rgb[2] * .950227f));


        xyz[0] = xyz[0] / 95.047f;
        xyz[1] = xyz[1] / 100.0f;
        xyz[2] = xyz[2] / 108.883f;

        if (xyz[0] > .008856f)
        {
            xyz[0] = (float)Math.Pow(xyz[0], (1.0 / 3.0));
        }
        else
        {
            xyz[0] = (xyz[0] * 7.787f) + (16.0f / 116.0f);
        }

        if (xyz[1] > .008856f)
        {
            xyz[1] = (float)Math.Pow(xyz[1], 1.0 / 3.0);
        }
        else
        {
            xyz[1] = (xyz[1] * 7.787f) + (16.0f / 116.0f);
        }

        if (xyz[2] > .008856f)
        {
            xyz[2] = (float)Math.Pow(xyz[2], 1.0 / 3.0);
        }
        else
        {
            xyz[2] = (xyz[2] * 7.787f) + (16.0f / 116.0f);
        }

        lab[0] = (116.0f * xyz[1]) - 16.0f;
        lab[1] = 500.0f * (xyz[0] - xyz[1]);
        lab[2] = 200.0f * (xyz[1] - xyz[2]);
        //Debug.Log("L:" + (int)lab[0]);
        //Debug.Log("A:" + (int)lab[1]);
        //Debug.Log("B:" + (int)lab[2]);
        //Debug.Log("W:" + (int)color[3]);

        return new Vector4(lab[0], lab[1], lab[2], color[3]);
    }
}