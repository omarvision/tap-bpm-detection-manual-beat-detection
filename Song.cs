using System.Collections;
using System.Collections.Generic;
using System.Linq;  //for linq lamda expressions
using System.IO;    //for creating debugging output file
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Song : MonoBehaviour
{
    #region --- helper ---
    private class PointData
    {
        public int idx = -1;
        public float time = -1;
        public int startsample = -1;
        public int endsample = -1;
        public float val = 0;
        public Vector3 vec3;
        public Vector3 vec3straight;
        public GameObject marker = null;
        public bool isSimple = false;
        public bool isBeat = false;
        public bool isTap = false;
        public bpmData calc;
        public bpmData tap;

        public string ToPoint(bool head)
        {
            System.Text.StringBuilder sb1 = new System.Text.StringBuilder();
            if (head == true)
            {
                sb1.Append("\n\t");
                sb1.Append("idx".PadRight(10));
                sb1.Append("time".PadRight(20));
                sb1.Append("val".PadRight(20));
                sb1.Append("startsample".PadRight(20));
                sb1.Append("endsample".PadRight(20));
                sb1.Append("delta".PadRight(20));
                sb1.Append("actualbpm".PadRight(20));
                sb1.Append("bpm".PadRight(20));

                sb1.Append("\n\t");
                sb1.Append("---------".PadRight(10));
                sb1.Append("-------------------".PadRight(20));
                sb1.Append("-------------------".PadRight(20));
                sb1.Append("-------------------".PadRight(20));
                sb1.Append("-------------------".PadRight(20));
                sb1.Append("-------------------".PadRight(20));
                sb1.Append("-------------------".PadRight(20));
                sb1.Append("-------------------".PadRight(20));
            }
            else
            {
                sb1.Append("\t");
                sb1.Append(idx.ToString().PadRight(10));
                sb1.Append(Lineup(time).PadRight(20));
                sb1.Append(Lineup(val).PadRight(20));
                sb1.Append(startsample.ToString().PadRight(20));
                sb1.Append(endsample.ToString().PadRight(20));
                sb1.Append(Lineup(calc.delta).PadRight(20));
                sb1.Append(Lineup(calc.actualbpm).PadRight(20));
                sb1.Append(Lineup(calc.bpm).PadRight(20));
            }
            return sb1.ToString();
        }
        public string Lineup(float val)
        {
            if (val == 0)
            {
                return "";
            }                
            else
            {
                string leftOfDecimal = val.ToString("F0");
                string rightOfDecimal = (Mathf.Abs(val % 1)).ToString(".0");
                return leftOfDecimal.PadLeft(6) + rightOfDecimal;
            }
        }
        public void ResetCalcData()
        {
            isSimple = false;
            isBeat = false;
            calc.delta = 0;
            calc.actualbpm = 0;
            calc.bpm = 0;
            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }
        }
    }
    private struct songData
    {
        public List<PointData> point;
        public PointData cp;
        public float calcbpm;
        public float tapbpm;
        public Material matBeat;
        public Material matTap;
        public Material matMark;
    }
    private struct bpmData
    {
        public float delta;
        public float actualbpm;
        public float bpm;
    }
    private struct childObject
    {
        public GameObject bottom;
        public GameObject start;
        public LineRenderer line;
        public LineRenderer simple;
        public GameObject cursor;
        public Camera camera;
        public TextMesh txtTime;
        public TextMesh txtBpm;
        public TextMesh txtTap;
        public GameObject holder;
    }
    #endregion

    public int samplesPerPoint = 1024;
    public float meterScale = 200;
    public float tolerance = 1;
    public float skipFwd = 2;
    public float skipRwd = 5;
    public float zoom = 25;
    public float quantizeHi = 160;
    public float quantizeLo = 81;
    private AudioSource aud = null;
    private songData song;
    private childObject obj;

    private void Start()
    {
        aud = this.GetComponent<AudioSource>();

        //child objects
        obj.bottom = GetTheChild("bottom", this.transform);
        obj.start = GetTheChild("start", this.transform);
        obj.line = GetTheChild("line", obj.start.transform).GetComponent<LineRenderer>();
        obj.simple = GetTheChild("simple", obj.start.transform).GetComponent<LineRenderer>();
        obj.cursor = GetTheChild("cursor", obj.start.transform);
        obj.camera = GetTheChild("camera", obj.cursor.transform).GetComponent<Camera>();
        obj.txtTime = GetTheChild("txttime", obj.cursor.transform).GetComponent<TextMesh>();
        obj.txtBpm = GetTheChild("txtbpm", obj.cursor.transform).GetComponent<TextMesh>();
        obj.txtTap = GetTheChild("txttap", obj.cursor.transform).GetComponent<TextMesh>();
        obj.holder = GetTheChild("holder", obj.start.transform);

        //samples summarized as point data
        float[] samples = new float[aud.clip.samples * aud.clip.channels];
        aud.clip.GetData(samples, 0);  //stero = l,r,l,r,l,r  mono=l,l,l,l,l
        song.point = new List<PointData>();
        song.point.Capacity = (int)(samples.Length / (float)samplesPerPoint + 1);
        int cnt = 0;
        float sum = 0;
        for (int s = 0; s < samples.Length; s += samplesPerPoint)
        {
            PointData pd = new PointData();
            pd.idx = cnt++;
            pd.startsample = pd.idx * samplesPerPoint;
            pd.endsample = ((pd.idx + 1) * samplesPerPoint) - 1;
            pd.time = (pd.startsample / aud.clip.channels) / (float)aud.clip.frequency;
            sum = 0;
            if (aud.clip.channels == 1)
            {
                for (int i = pd.startsample; i <= pd.endsample; i += aud.clip.channels)
                {
                    if (i > samples.Length - 1)
                        break;
                    sum += samples[i];
                }
            }
            else if (aud.clip.channels == 2)
            {
                for (int i = pd.startsample; i <= pd.endsample; i += aud.clip.channels)
                {
                    if (i > samples.Length - 2)
                        break;
                    sum += (samples[i] + samples[i + 1]) * 0.5f;
                }
            }
            pd.val = Mathf.Abs((sum / samplesPerPoint) * meterScale);
            pd.vec3 = new Vector3(pd.idx, pd.val, 0);
            pd.vec3straight = new Vector3(pd.idx, 0, 0);
            song.point.Add(pd);
        }
        song.matBeat = new Material(Shader.Find("Standard"));
        song.matBeat.color = Color.black;
        song.matTap = new Material(Shader.Find("Standard"));
        song.matTap.color = Color.cyan;
        song.matMark = new Material(Shader.Find("Standard"));
        song.matMark.color = Color.yellow;

        //bottom start
        obj.bottom.transform.localScale = new Vector3(song.point.Count, obj.bottom.transform.localScale.y, obj.bottom.transform.localScale.z);
        obj.start.transform.localPosition = new Vector3(song.point.Count * -0.5f, 0, 0);

        //waveform
        obj.line.positionCount = song.point.Count;
        obj.line.SetPositions(song.point.Select(x => x.vec3).ToArray());

        Debugging();
    }
    private void Update()
    {
        float timesample = aud.timeSamples * aud.clip.channels;
        song.cp = song.point.Single(x => x.startsample <= timesample && timesample <= x.endsample);
        obj.cursor.transform.localPosition = song.cp.vec3straight;
        obj.txtTime.text = "time:" + song.cp.time.ToString("0.00");

        KeyboardPlayback();
        KeyboardTap();
    }
    private void KeyboardPlayback()
    {
        //play
        if (Input.GetKeyDown(KeyCode.Alpha0) == true)
        {
            aud.time = 0;
            aud.Play();
        }

        //pause
        if (Input.GetKeyDown(KeyCode.P) == true)
        {
            if (aud.isPlaying == true)
            {
                aud.Pause();
            }
            else
            {
                aud.UnPause();
            }
        }

        //rwd, fwd
        if (Input.GetKey(KeyCode.Comma) == true)
        {
            bool playing = aud.isPlaying;
            if (playing == false) aud.Play();
            aud.time = Mathf.Clamp(aud.time - (skipRwd * Time.deltaTime), 0, aud.time - (skipRwd * Time.deltaTime));
            if (playing == false) aud.Stop();
        }
        else if (Input.GetKey(KeyCode.Period) == true)
        {
            bool playing = aud.isPlaying;
            if (playing == false) aud.Play();
            aud.time = Mathf.Clamp(aud.time + (skipFwd * Time.deltaTime), aud.time + (skipFwd * Time.deltaTime), aud.clip.length);
            if (playing == false) aud.Stop();
        }

        //zoom
        if (Input.GetKey(KeyCode.Semicolon) == true)
        {
            float sz = obj.camera.orthographicSize - (zoom * Time.deltaTime);
            obj.camera.orthographicSize = Mathf.Clamp(sz, 1, 299);
        }
        else if (Input.GetKey(KeyCode.Quote) == true)
        {
            float sz = obj.camera.orthographicSize + (zoom * Time.deltaTime);
            obj.camera.orthographicSize = Mathf.Clamp(sz, 1, 299);
        }
    }
    private void KeyboardTap()
    {
        if (Input.GetKeyDown(KeyCode.Space) == true)
        {
            song.cp.isTap = true;
            CreateMarker(ref song.cp, song.matTap, "tap");

            //what is tap bpm?
            List<PointData> taps = song.point.Where(x => x.isTap == true).ToList();
            if (taps.Count > 1)
            {
                for (int i = 1; i < taps.Count; i++)
                {
                    taps[i].tap.delta = taps[i].time - taps[i - 1].time;
                    taps[i].tap.actualbpm = 60f / taps[i].tap.delta;
                    taps[i].tap.bpm = QuantizeBpm(taps[i].tap.actualbpm);
                }
                song.tapbpm = taps.Where(x => x.tap.bpm > 0).Average(x => x.tap.bpm);
                obj.txtTap.text = "tap:" + song.tapbpm.ToString("0.00");
            }
        }        
    }
    //
    private GameObject GetTheChild(string name, Transform parent)
    {
        foreach(Transform child in parent)
        {
            if (child.gameObject.name.ToLower() == name.ToLower())
            {
                return child.gameObject;
            }
        }
        return null;
    }
    private float QuantizeBpm(float bpm)
    {
        if (bpm == 0)
            return bpm;
        if (bpm > quantizeHi)
        {
            while (bpm > quantizeHi)
                bpm *= 0.5f;

        }
        else if (bpm < quantizeLo)
        {
            while (bpm < quantizeLo)
                bpm *= 2.0f;
        }

        return bpm;
    }
    private void CreateMarker(ref PointData pd, Material mat, string name)
    {
        if (pd.marker != null)
            return;
        pd.marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pd.marker.transform.parent = obj.holder.transform;
        pd.marker.transform.localPosition = pd.vec3straight;
        pd.marker.transform.localScale = new Vector3(1, 50, 1);
        pd.marker.GetComponent<Renderer>().material = mat;
        pd.marker.name = name;
    }
    private void Debugging()
    {
        string path = "Assets/Debug.txt";
        StreamWriter sw = new StreamWriter(path, false);

        sw.WriteLine("\nSong:");
        sw.WriteLine(string.Format("\tsamples = {0}", aud.clip.samples));
        sw.WriteLine(string.Format("\tchannels = {0}", aud.clip.channels));
        sw.WriteLine(string.Format("\ttotalsamples = {0}", aud.clip.samples * aud.clip.channels));
        sw.WriteLine(string.Format("\tsamples per point = {0}", samplesPerPoint));
        sw.WriteLine(string.Format("\tmeterScale = {0}", meterScale));
        sw.WriteLine(string.Format("\ttolerance = {0}", tolerance));
        sw.WriteLine(string.Format("\tquantize bpm = {0} thru {1}", quantizeLo, quantizeHi));

        sw.WriteLine(string.Format("\nPoints:{0}", song.point.Count));
        sw.WriteLine(song.point[0].ToPoint(true));
        foreach (PointData pd in song.point)
        {
            sw.WriteLine(pd.ToPoint(false));
        }

        sw.Close();
        UnityEditor.AssetDatabase.ImportAsset(path);
    }
}
