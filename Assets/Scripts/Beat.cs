using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using System;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class Beat : MonoBehaviour
{

    #region ----- helper -----
    private class PointData
    {
        public int index = -1;
        public float time = -1;
        public int startSample = -1;
        public int endSample = -1;
        public float value = 0;
        public bool isSimple = false;
        public bool isBeat = false;
        public Vector3 vector3;
        public Vector3 vector3_Straight;
        public GameObject marker = null;
        public BeatData beat;

        public string ToPoint(bool head)
        {
            System.Text.StringBuilder sb1 = new System.Text.StringBuilder();

            if (head)
            {
                sb1.Append("index".PadRight(10));
                sb1.Append("time".PadRight(20));
                sb1.Append("start sample".PadRight(20));
                sb1.Append("end sample".PadRight(20));
                sb1.Append("value".PadRight(20));
                sb1.Append("delta".PadRight(20));
                sb1.Append("actual BPM".PadRight(20));
                sb1.Append("BPM".PadRight(20));
            }
            else
            {
                sb1.Append(index.ToString().PadRight(10));
                sb1.Append(LineupDecimal(time).PadRight(20));
                sb1.Append(startSample.ToString().PadRight(20));
                sb1.Append(endSample.ToString().PadRight(20));
                sb1.Append(LineupDecimal(value).PadRight(20));
                sb1.Append(LineupDecimal(beat.delta).PadRight(20));
                sb1.Append(LineupDecimal(beat.actualBPM).PadRight(20));
                sb1.Append(LineupDecimal(beat.BPM).PadRight(20));
            }

            return sb1.ToString();
        }
        private string LineupDecimal(float value)
        {
            if (value == 0)
            {
                return "";
            }
            else
            {
                string left = value.ToString("F0");
                string right = (Mathf.Abs(value % 1)).ToString(".0000");
                return left.PadLeft(6) + right;
            }
        }
    }

    private struct ClipData
    {
        public float[] samples;
        public List<PointData> points;
        public PointData currentPos;
        public float BPM;
    }

    private struct BeatData
    {
        public float delta;
        public float actualBPM;
        public float BPM;
    }

    private struct ChildObject
    {
        public GameObject bottom;
        public GameObject start;
        public GameObject cursor;
        public GameObject beats;
        public LineRenderer line;
        public LineRenderer simple;
    }
    #endregion

    public int samplesPerPoint = 1024;
    public float meterScale = 100f;
    public float zoom = 1f;
    public float skipForward = 1f;
    public float skipBackward = 5f;
    public float tolerance = 20f;
    public float QUANTIZE_HIGH = 160f;
    public float QUANTIZE_LOW = 80f;

    public static float[] freqBands;

    private AudioSource audio = null;
    private ClipData clip;
    private ChildObject childObject;

    public TextMeshProUGUI timeText;
    public TextMeshProUGUI sampleText;
    public TextMeshProUGUI indexText;
    public TextMeshProUGUI hertzText;
    public TextMeshProUGUI nameText;

    public TextMeshProUGUI beatsText;
    public TextMeshProUGUI deltaText;
    public TextMeshProUGUI generalBPMText;
    public TextMeshProUGUI currentBPMText;
    public TextMeshProUGUI currentActualBPMText;

    public GameObject marker;

    private Color custom_red = new Color32(192, 57, 43, 255);
    private Color custom_green = new Color32(39, 174, 96, 255);

    private void Start()
    {
        freqBands = new float[8];

        audio = GetComponent<AudioSource>();
        GetPoints();
        SetPointData();
        GetChildObjects();
        DisplayWaveForm();
        SetBottomLine();
        SimpleLine();
        FindBeats();
        Debugging();

        beatsText.text = "Beats: " + GetBeats().Count.ToString();
        generalBPMText.text = "Gen BPM: " + clip.BPM.ToString();
        hertzText.text = "Freq: " + audio.clip.frequency;
        nameText.text = audio.clip.name;
    }

    private void Update()
    {
        SetPointCurrentPosition();
        MoveCursor();
        SetInfoTexts();
        CheckForMouseZoom();
        CheckForPlayTimeUpdates();
        CheckForStopUpdates();
    }

    private void SimpleLine()
    {
        List<int> keep = new List<int>();
        LineUtility.Simplify(clip.points.Select(x => x.vector3).ToList(), tolerance, keep);

        for (int k = 0; k < keep.Count; k++)
        {
            int index = keep[k];
            clip.points[index].isSimple = true;
        }

        childObject.simple.positionCount = keep.Count;
        childObject.simple.SetPositions(clip.points.Where(x => x.isSimple == true).Select(x => x.vector3).ToArray());
    }

    private void FindBeats()
    {
        List<PointData> simple = clip.points.Where(x => x.isSimple == true).ToList();

        for (int i = 0; i < simple.Count; i++)
        {
            if (i == 0)
            {
                if (simple[i].value > simple[i + 1].value)
                    simple[i].isBeat = true;
            }
            else if (i == simple.Count - 1)
            {
                if (simple[i].value > simple[i - 1].value)
                    simple[i].isBeat = true;
            }
            else
            {
                if (simple[i - 1].value < simple[i].value && simple[i].value > simple[i + 1].value)
                    simple[i].isBeat = true;
            }
        }

        MarkBeats();
        GetBPMFromBeat();
    }

    private void GetBPMFromBeat()
    {
        List<PointData> beats = GetBeats();

        for (int i = 1; i < beats.Count; i++)
        {
            beats[i].beat.delta = beats[i].time - beats[i - 1].time;
            beats[i].beat.actualBPM = 60f / beats[i].beat.delta;
            beats[i].beat.BPM = QuantizeBPM(beats[i].beat.actualBPM);
        }

        clip.BPM = beats.Average(x => x.beat.BPM);
    }

    private void MarkBeats()
    {
        List<PointData> beats = GetBeats();
        for (int i = 0; i < beats.Count; i++)
        {
            if (beats[i].marker == null)
            {
                beats[i].marker = CreateMarker(beats[i].vector3_Straight);
            }
        }
    }

    private List<PointData> GetBeats()
    {
        List<PointData> beats = clip.points.Where(x => x.isBeat == true).ToList();
        return beats;
    }

    private GameObject CreateMarker(Vector3 pos)
    {
        GameObject quad = Instantiate(marker);
        quad.transform.parent = childObject.beats.transform;
        quad.transform.localPosition = pos;
        quad.transform.localScale = new Vector3(.1f, 30, 1);
        quad.name = name;

        return quad;
    }

    private void CheckForStopUpdates()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            PlayAudio(this.audio);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartAudio(this.audio);
        }
    }

    private void CheckForPlayTimeUpdates()
    {
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            bool playing = audio.isPlaying;
            if (!playing) audio.Play();

            audio.time = Mathf.Clamp(audio.time - (skipBackward * Time.deltaTime), 0, audio.time - (skipBackward * Time.deltaTime));
            if (!playing) audio.Stop();
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            bool playing = audio.isPlaying;
            if (!playing) audio.Play();

            audio.time = Mathf.Clamp(audio.time + (skipForward * Time.deltaTime), audio.time + (skipForward * Time.deltaTime), clip.points[clip.points.Count - 1].time);
            if (!playing) audio.Stop();
        }
    }

    private void CheckForMouseZoom()
    {
        Camera.main.orthographicSize += Input.mouseScrollDelta.y * zoom * -1;
    }

    private void SetInfoTexts()
    {
        Color color = Mathf.Abs(clip.currentPos.value) > 1.5f ? custom_red : custom_green;
        sampleText.color = color;

        timeText.text = "Time (s): " + audio.clip.length + " / " + Math.Round((clip.currentPos.time), 2).ToString();
        sampleText.text = "Sample: " + clip.currentPos.value.ToString();
        indexText.text = "Index: " + clip.currentPos.index.ToString();

        if (clip.currentPos.isBeat)
        {
            deltaText.text = "Delta: " + clip.currentPos.beat.delta.ToString();
            currentBPMText.text = "Curr BPM: " + clip.currentPos.beat.BPM.ToString();
            currentActualBPMText.text = "Act BPM: " + clip.currentPos.beat.actualBPM.ToString();
        }
        
    }

    private void MoveCursor()
    {
        childObject.cursor.transform.localPosition = clip.currentPos.vector3_Straight;
    }

    private void SetPointCurrentPosition()
    {
        float timeSample = audio.timeSamples * audio.clip.channels;
        clip.currentPos = clip.points.Single(x => x.startSample <= timeSample && x.endSample > timeSample);
    }

    private void SetBottomLine()
    {
        childObject.bottom.transform.localScale = new Vector3(clip.points.Count(), childObject.bottom.transform.localScale.y, childObject.bottom.transform.localScale.z);
        childObject.start.transform.localPosition = new Vector3(childObject.bottom.transform.localScale.x * -.5f,
            childObject.start.transform.localPosition.y,
            childObject.start.transform.localPosition.z);
    }

    private void DisplayWaveForm()
    {
        childObject.line.positionCount = clip.points.Count();
        childObject.line.SetPositions(clip.points.Select(x => x.vector3).ToArray());
    }

    private void GetChildObjects()
    {
        childObject.bottom = GetTheChild("bottom", transform);
        childObject.start = GetTheChild("start", transform);
        childObject.line = GetTheChild("line", childObject.start.transform).GetComponent<LineRenderer>();
        childObject.cursor = GetTheChild("cursor", childObject.start.transform);
        childObject.simple = GetTheChild("simple", childObject.start.transform).GetComponent<LineRenderer>();
        childObject.beats = GetTheChild("beats", childObject.start.transform);
    }

    private void SetPointData()
    {
        float sum = 0;
        int count = 0;
        clip.points = new List<PointData>();
        clip.points.Capacity = clip.samples.Length / samplesPerPoint + 1;
        for (int s = 0; s < clip.samples.Length; s += samplesPerPoint)
        {
            PointData pd = new PointData();
            pd.index = count++;
            pd.startSample = pd.index * samplesPerPoint; //1*1024--1024 2*1024--2048 3*1024--3072
            pd.endSample = ((pd.index + 1) * samplesPerPoint) - 1; // 2*1024-1--2047 3*1024-1--3071 4*1024-1--4096
            pd.time = (pd.startSample / (float)audio.clip.channels) / (float)audio.clip.frequency;

            sum = 0;
            if (audio.clip.channels == 1)
            {
                //mono
                for (int i = pd.startSample; i <= pd.endSample; i++)
                {
                    if (i > clip.samples.Length - 1)
                        break;

                    sum += clip.samples[i];
                }
            }
            else
            {
                //stereo
                for (int i = pd.startSample; i <= pd.endSample; i += audio.clip.channels)
                {
                    if (i > clip.samples.Length - 1)
                        break;

                    sum += (clip.samples[i] + clip.samples[i + 1]) * .5f;
                }
            }

            pd.value = (sum / samplesPerPoint) * meterScale;
            pd.vector3 = new Vector3(pd.index, pd.value, 0);
            pd.vector3_Straight = new Vector3(pd.index, 0, 0);

            clip.points.Add(pd);

        }
    }

    private void GetPoints()
    {
        clip.samples = new float[audio.clip.samples * audio.clip.channels];
        audio.clip.GetData(clip.samples, 0);
    }

    private GameObject GetTheChild(string name, Transform parent)
    {
        foreach (Transform child in parent)
            if (child.name.ToLower() == name.ToLower())
                return child.gameObject;

        return null;
    }

    private void Debugging()
    {
        string path = "Assets/Debug.txt";
        StreamWriter sw = new StreamWriter(path, false);

        sw.WriteLine("Song:");
        sw.WriteLine(string.Format("\tsamples={0}", audio.clip.samples));
        sw.WriteLine(string.Format("\tchannels={0}", audio.clip.channels));
        sw.WriteLine(string.Format("\tBPM={0}", clip.BPM));

        sw.WriteLine(string.Format("\n\tBeat:({0})", clip.points.Where(x => x.isBeat == true).ToList().Count));
        sw.WriteLine("\t" + clip.points[0].ToPoint(true));
        foreach (PointData point in clip.points.Where(x => x.isBeat == true).ToList())
        {
            sw.WriteLine("\t" + point.ToPoint(false));
        }

        sw.WriteLine(string.Format("\n\tSimple:({0})", clip.points.Where(x => x.isSimple == true).ToList().Count));
        sw.WriteLine("\t" + clip.points[0].ToPoint(true));
        foreach (PointData point in clip.points.Where(x => x.isSimple == true).ToList())
        {
            sw.WriteLine("\t" + point.ToPoint(false));
        }

        sw.WriteLine(string.Format("\n\tPoints:({0})", clip.points.Count));
        sw.WriteLine("\t" + clip.points[0].ToPoint(true));
        foreach (PointData point in clip.points)
        {
            sw.WriteLine("\t" + point.ToPoint(false));
        }

        sw.Close();
        UnityEditor.AssetDatabase.ImportAsset(path);
    }

    private float QuantizeBPM(float actualBPM)
    {
        float normalisedBPM = Mathf.Clamp(actualBPM, QUANTIZE_LOW, QUANTIZE_HIGH);
        return normalisedBPM;
    }

    private void PlayAudio(AudioSource audio)
    {
        if (audio.isPlaying)
            audio.Pause();
        else
            audio.Play();
    }

    private void RestartAudio(AudioSource audio)
    {
        bool wasPlaying = audio.isPlaying;

        audio.Stop();

        if (wasPlaying)
            PlayAudio(audio);
    }
}
