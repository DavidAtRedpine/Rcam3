using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using Lays = Unity.Sentis.Layers;
using System.IO;
using FF = Unity.Sentis.Functional;

/*
 *  LaboroTomato (made with YoloV8) Inference Script
 *  ========================
 * 
 * Place this script on the Main Camera.
 * 
 * Place the laboro_tomato_yolov8.sentis file in the asset folder and drag onto the asset field
 * Place a *.mp4 video file in the Assets/StreamingAssets folder
 * Create a RawImage in your scene and set it as the displayImage field
 * Drag the classes.txt into the labelsAsset field
 * Add a reference to a sprite image for the bounding box and a font for the text
 * 
 */


public class RunLaboroTomato : MonoBehaviour
{
    // Drag the yolov8n.sentis file here
    public ModelAsset asset;
    // Link the classes.txt here:
    public TextAsset labelsAsset;
    // Image to feed into the model
    public RenderTexture inputRenderTexture;

    // Create a Raw Image in the scene and link it here:
    public RawImage displayImage;
    // Link to a bounding box sprite or texture here:
    public Sprite borderSprite;
    public Texture2D borderTexture;
    // Link to the font for the labels:
    public Font font;

    const BackendType backend = BackendType.GPUCompute;

    private Transform displayLocation;
    private IWorker engine;
    private string[] labels;
    private RenderTexture targetRT;


    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    //The number of classes in the model
    private const int numClasses = 80;

    List<GameObject> boxPool = new();

    [SerializeField, Range(0, 1)] float iouThreshold = 0.5f;
    [SerializeField, Range(0, 1)] float scoreThreshold = 0.5f;
    int maxOutputBoxes = 64;

    TensorFloat centersToCorners;
    //bounding box data
    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
    }

    void Start()
    {
        Application.targetFrameRate = 60;

        //Parse neural net labels
        labels = labelsAsset.text.Split('\n');

        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);

        //Create image to display output
        displayLocation = displayImage.transform;

        if (borderSprite == null)
        {
            borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));
        }
    }
    void LoadModel()
    {

        //Load model
        var model1 = ModelLoader.Load(asset);

        centersToCorners = new TensorFloat(new TensorShape(4, 4),
        new float[]
        {
                    1,      0,      1,      0,
                    0,      1,      0,      1,
                    -0.5f,  0,      0.5f,   0,
                    0,      -0.5f,  0,      0.5f
        });

        //Here we transform the output of the model1 by feeding it through a Non-Max-Suppression layer.
        var model2 = Functional.Compile(
               input =>
               {
                   var modelOutput = model1.Forward(input)[0];
                   var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);        //shape=(8400,4)
                   var allScores = modelOutput[0, 4.., ..];                         //shape=(80,8400)
                   var scores = FF.ReduceMax(allScores, 0) - scoreThreshold;        //shape=(8400)
                   var classIDs = FF.ArgMax(allScores, 0);                          //shape=(8400) 
                   var boxCorners = FF.MatMul(boxCoords, FunctionalTensor.FromTensor(centersToCorners));
                   var indices = FF.NMS(boxCorners, scores, iouThreshold);           //shape=(N)
                   var indices2 = indices.Unsqueeze(-1).BroadcastTo(new int[] { 4 });//shape=(N,4)
                   var coords = FF.Gather(boxCoords, 0, indices2);                  //shape=(N,4)
                   var labelIDs = FF.Gather(classIDs, 0, indices);                  //shape=(N)
                   return (coords, labelIDs);
               },
               InputDef.FromModel(model1)[0]
         );

        //Create engine to run model
        engine = WorkerFactory.CreateWorker(backend, model2);
    }

    private void Update()
    {
        ExecuteML();
    }

    public void ExecuteML()
    {
        ClearAnnotations();

        if (!inputRenderTexture) return;

        using var input = TextureConverter.ToTensor(inputRenderTexture, imageWidth, imageHeight, 3);
        engine.Execute(input);

        var output = engine.PeekOutput("output_0") as TensorFloat;
        var labelIDs = engine.PeekOutput("output_1") as TensorInt;

        output.CompleteOperationsAndDownload();
        labelIDs.CompleteOperationsAndDownload();

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        int boxesFound = output.shape[0];
        //Draw the bounding boxes
        for (int n = 0; n < Mathf.Min(boxesFound, maxOutputBoxes); n++)
        {
            var box = new BoundingBox
            {
                centerX = output[n, 0] * scaleX - displayWidth / 2,
                centerY = output[n, 1] * scaleY - displayHeight / 2,
                width = output[n, 2] * scaleX,
                height = output[n, 3] * scaleY,
                label = labels[labelIDs[n]],
            };
            DrawBox(box, n, displayHeight * 0.05f);
        }
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        //Create the bounding box graphic or get from pool
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }
        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        var col = GetBoxColor(box.label);

        //Set label text
        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;
        label.color = col;

        //Set color
        var img = panel.GetComponentInChildren<Image>();
        img.color = col;
    }

    private Color GetBoxColor(string label)
    {
        if(label == "b_green" || label == "l_green")
        {
            return Color.green;
        }
        if(label == "b_half_ripened" || label == "l_half_ripened")
        {
            return Color.yellow;
        }

        return Color.red;
    }

    public GameObject CreateNewBox(Color color)
    {
        //Create the box and set image

        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        //Create the label

        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.fontSize = 40;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach (var box in boxPool)
        {
            box.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        centersToCorners?.Dispose();
        engine?.Dispose();
    }
}