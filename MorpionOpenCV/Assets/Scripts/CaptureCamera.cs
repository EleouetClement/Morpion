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
        Vector3 minDistance = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        GameObject match = null;
        if(!(gridComponents == null || gridComponents.Count == 0))
        {
            foreach (var comp in gridComponents)
            {
                var compColor = comp.GetComponent<Renderer>().material.color;
                var distance = new Vector3(Mathf.Abs(color.x - compColor.r), Mathf.Abs(color.y - compColor.g), Mathf.Abs(color.z - compColor.b));
                if (distance.x < minDistance.x && distance.y < minDistance.y && distance.z < minDistance.z)
                {
                    minDistance = distance;
                    match = comp;
                }
            }
            if (match != null && !gridFlags[match])
            {
                gridFlags[match] = true;
                return match;
            }
        }
        return null;
    }
    private System.Drawing.Color GetSystemDrawingColorFromHexString(string hexString)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(hexString, @"[#]([0-9]|[a-f]|[A-F]){6}\b"))
            throw new ArgumentException();
        int red = int.Parse(hexString.Substring(1, 2), NumberStyles.HexNumber);
        int green = int.Parse(hexString.Substring(3, 2), NumberStyles.HexNumber);
        int blue = int.Parse(hexString.Substring(5, 2), NumberStyles.HexNumber);
        return System.Drawing.Color.FromArgb(red, green, blue);
    }
    private string GetColor(string colorCode)
    {
        System.Drawing.Color color = GetSystemDrawingColorFromHexString(colorCode);
        return color.Name;
    }
}