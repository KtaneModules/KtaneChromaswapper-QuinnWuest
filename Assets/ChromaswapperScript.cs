using KModkit;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using Rnd = UnityEngine.Random;

public class ChromaswapperScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public GameObject[] ButtonObjs;
    public GameObject[] BkgdObjs;
    private readonly KMSelectable[] ButtonSels = new KMSelectable[36];

    public GameObject Pivot;
    public Material[] ColorMats;
    public TextMesh[] Texts;
    public TextMesh[] OuterTexts;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private CellColor[] _colorGridSolution;
    private CellColor[] _colorGridCurrent;
    private int[] _numberGrid;
    private Operation[] _operations;
    private string _priorityString;

    private bool _isAnimating;
    private readonly Vector3[] _buttonPositions = new Vector3[36];
    private int? _currentlySelectedButton = null;

    public class VoltData { public string voltage { get; set; } }

    private static readonly string[] _ternaries = new string[36]
    {
        "0000", "0001", "0002", "0010", "0011", "0012", "0020", "0021", "0022", "0100", "0101", "0102", "0110", "0111", "0112", "0120", "0121", "0122", "0200", "0201", "0202", "0210", "0211", "0212", "0220", "0221", "0222", "1000", "1001", "1002", "1010", "1011", "1012", "1020", "1021", "1022"
    };

    private enum CellColor
    {
        Red,
        Yellow,
        Blue
    }

    private enum Operation
    {
        LessThan,
        EqualTo,
        GreaterThan
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ButtonObjs.Length; i++)
        {
            ButtonSels[i] = ButtonObjs[i].GetComponent<KMSelectable>();
            ButtonSels[i].OnInteract += ButtonPress(i);
            _buttonPositions[i] = ButtonObjs[i].transform.localPosition;
        }

        tryAgain:
        _colorGridSolution = new int[36].Select(i => (CellColor)Rnd.Range(0, 3)).ToArray();
        if (_colorGridSolution.Distinct().Count() != 3 || Enumerable.Range(0, 3).Select(i => _colorGridSolution.Count(j => j == (CellColor)i)).Any(i => i < 8))
            goto tryAgain;

        _numberGrid = Enumerable.Range(0, 36).Select(ix => GetAdjacents(ix).Count(adj => _colorGridSolution[ix] == _colorGridSolution[adj])).ToArray();
        if (_numberGrid.All(i => i < 5) || !_numberGrid.Contains(0))
            goto tryAgain;
        for (int i = 0; i < 36; i++)
            Texts[i].text = _numberGrid[i].ToString();

        _colorGridCurrent = _colorGridSolution.ToArray();
        while (IsCurrentGridCorrect())
            _colorGridCurrent.Shuffle();

        SetColors();
        Debug.LogFormat("[Chromaswapper #{0}] Digits: {1}", _moduleId, _numberGrid.Join(""));
        Debug.LogFormat("[Chromaswapper #{0}] Colors: {1} Red, {2} Yellow, {3} Blue", _moduleId, _colorGridSolution.Count(i => i == CellColor.Red), _colorGridSolution.Count(i => i == CellColor.Yellow), _colorGridSolution.Count(i => i == CellColor.Blue));
        Debug.LogFormat("[Chromaswapper #{0}] Possible solution: {1}", _moduleId, _colorGridSolution.Select(i => i.ToString()[0]).Join(""));

        if (BombInfo.IsIndicatorOn("BOB") && BombInfo.GetPortPlates().ToArray().Select(i => i.Join(" ")).Any(i => i == "DVI RJ45"))
            _priorityString = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        else if (Enumerable.Range(0, 6).Select(i => new[] { _numberGrid[i], _numberGrid[i + 6], _numberGrid[i + 12], _numberGrid[i + 18], _numberGrid[i + 24], _numberGrid[i + 30] }).Any(i => i.Sum() >= 23))
            _priorityString = "1QAZ2WSX3EDC4RFV5TGB6YHN7UJM8IK9OL0P";
        else if (BombInfo.QueryWidgets("volt", "").Count != 0 && float.Parse(JsonConvert.DeserializeObject<VoltData>(BombInfo.QueryWidgets("volt", "").First()).voltage) >= 3.5f)
            _priorityString = "SPEAKING3V1LBCDFHJMOQRTUWXYZ02456789";
        else if (BombInfo.GetModuleNames().Count(i => i.ToLowerInvariant().Contains("swap")) > 1)
            _priorityString = "WATCH98JEOPRDY76LX54BKS32FUN10VQIZGM";
        else if (BombInfo.GetPorts().Distinct().Count() >= 4)
            _priorityString = "36THE9FIV2BOXNG5WZARDS80JUMP1QCKLY47";
        else if (BombInfo.QueryWidgets("hi", "").Count() != 0)
            _priorityString = "HIWDGET1029384756ZYXVUSRQPONMLKJFCBA";
        else if (BombInfo.GetBatteryCount() == 0 || ((double)BombInfo.GetBatteryHolderCount() / BombInfo.GetBatteryCount() == 0.75))
            _priorityString = "WXZUN4CO7PY1RI8GH5TA3BL6ED0FJ1KM9QSV";
        else if (Enumerable.Range(0, 6).Select(i => new[] { _numberGrid[i * 6], _numberGrid[i * 6 + 1], _numberGrid[i * 6 + 2], _numberGrid[i * 6 + 3], _numberGrid[i * 6 + 4], _numberGrid[i * 6 + 5] }).Any(i => i.Sum() <= 6))
            _priorityString = "1THE3QUICK5BROWN7FX9JMPS0V24LAZY6DG8";
        else
            _priorityString = "0PLO98IKMJU76YHNBGT54RFVCDE32WSXZAQ1";
        var list = new List<char>();
        var sn = BombInfo.GetSerialNumber();
        for (int i = 0; i < _priorityString.Length; i++)
            for (int j = 0; j < sn.Count(x => x == _priorityString[i]); j++)
                list.Add(_priorityString[i]);
        var arrs = new int[12][]
        {
            new int[6]{0, 6, 12, 18, 24, 30},
            new int[6]{1, 7, 13, 19, 25, 31},
            new int[6]{2, 8, 14, 20, 26, 32},
            new int[6]{3, 9, 15, 21, 27, 33},
            new int[6]{4, 10, 16, 22, 28, 34},
            new int[6]{5, 11, 17, 23, 29, 35},
            new int[6]{0, 1, 2, 3, 4, 5},
            new int[6]{6, 7, 8, 9, 10, 11},
            new int[6]{12, 13, 14, 15, 16, 17},
            new int[6]{18, 19, 20, 21, 22, 23},
            new int[6]{24, 25, 26, 27, 28, 29},
            new int[6]{30, 31, 32, 33, 34, 35}
        };
        _operations = new Operation[12];
        var final = list.Take(3).Select(i => _ternaries[i >= '0' && i <= '9' ? (i - '0') : (i - 'A' + 10)]).Join("");
        for (int i = 0; i < arrs.Length; i++)
        {
            if (final[i] == '0' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Red) < 3)
                _operations[i] = Operation.LessThan;
            else if (final[i] == '0' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Red) == 3)
                _operations[i] = Operation.EqualTo;
            else if (final[i] == '0' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Red) > 3)
                _operations[i] = Operation.GreaterThan;
            else if (final[i] == '1' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Yellow) < 3)
                _operations[i] = Operation.LessThan;
            else if (final[i] == '1' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Yellow) == 3)
                _operations[i] = Operation.EqualTo;
            else if (final[i] == '1' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Yellow) > 3)
                _operations[i] = Operation.GreaterThan;
            else if (final[i] == '2' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Blue) < 3)
                _operations[i] = Operation.LessThan;
            else if (final[i] == '2' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Blue) == 3)
                _operations[i] = Operation.EqualTo;
            else if (final[i] == '2' && arrs[i].Count(j => _colorGridSolution[j] == CellColor.Blue) > 3)
                _operations[i] = Operation.GreaterThan;
            else
                throw new InvalidOperationException("i fucked up");
        }
        for (int i = 0; i < 12; i++)
            OuterTexts[i].text = "<=>"[(int)_operations[i]].ToString();
        Debug.LogFormat("[Chromaswapper #{0}] Operations: {1}.", _moduleId, Enumerable.Range(0, 12).Select(i => "<=>"[(int)_operations[i]]).Join(""));
        Debug.LogFormat("[Chromaswapper #{0}] Priority string: {1}", _moduleId, _priorityString);
        Debug.LogFormat("[Chromaswapper #{0}] Chosen serial number characters are {1}. Ternary is {2}.", _moduleId, list.Take(3).Join(", "), final);
    }

    private KMSelectable.OnInteractHandler ButtonPress(int i)
    {
        return delegate ()
        {
            if (_moduleSolved || _isAnimating)
                return false;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, ButtonSels[i].transform);
            ButtonSels[i].AddInteractionPunch(0.25f);
            var tr = ButtonObjs[i].transform.localPosition;
            if (_currentlySelectedButton == null)
            {
                _currentlySelectedButton = i;
                BkgdObjs[i].GetComponent<MeshRenderer>().sharedMaterial = ColorMats[4];
                ButtonObjs[i].transform.localPosition = new Vector3(tr.x, 0.004f, tr.z);
            }
            else if (_currentlySelectedButton == i)
            {
                _currentlySelectedButton = null;
                BkgdObjs[i].GetComponent<MeshRenderer>().sharedMaterial = ColorMats[3];
                ButtonObjs[i].transform.localPosition = new Vector3(tr.x, 0f, tr.z);
            }
            else
            {
                BkgdObjs[i].GetComponent<MeshRenderer>().sharedMaterial = ColorMats[4];
                ButtonObjs[i].transform.localPosition = new Vector3(tr.x, 0.004f, tr.z);
                StartCoroutine(Swap(i, _currentlySelectedButton.Value));
                _currentlySelectedButton = null;
            }
            return false;
        };
    }

    private IEnumerable<int> GetAdjacents(int num)
    {
        if (num % 6 != 0)
            yield return num - 1;
        if (num % 6 != 5)
            yield return num + 1;
        if (num / 6 != 0)
            yield return num - 6;
        if (num / 6 != 5)
            yield return num + 6;
        if (num % 6 != 0 && num / 6 != 0)
            yield return num - 7;
        if (num % 6 != 0 && num / 6 != 5)
            yield return num + 5;
        if (num % 6 != 5 && num / 6 != 0)
            yield return num - 5;
        if (num % 6 != 5 && num / 6 != 5)
            yield return num + 7;
    }

    private IEnumerator Swap(int a, int b)
    {
        _isAnimating = true;
        float t = 0;
        float cutoff = Time.deltaTime;
        Texts[a].color = new Color32(255, 255, 255, 255);
        Texts[b].color = new Color32(255, 255, 255, 255);
        Pivot.transform.localPosition = (_buttonPositions[a] + _buttonPositions[b]) / 2;
        while (t < 0.5f - cutoff)
        {
            float del = Time.deltaTime;
            t += del;
            ButtonObjs[a].transform.RotateAround(Pivot.transform.position, transform.up, del * 360);
            ButtonObjs[b].transform.RotateAround(Pivot.transform.position, transform.up, del * 360);
            ButtonObjs[a].transform.localRotation = Quaternion.Euler(0, 0, 0);
            ButtonObjs[b].transform.localRotation = Quaternion.Euler(0, 0, 0);
            yield return null;
        }
        var posA = _buttonPositions[a];
        var posB = _buttonPositions[b];
        var colorA = _colorGridCurrent[a];
        var colorB = _colorGridCurrent[b];
        _colorGridCurrent[a] = colorB;
        _colorGridCurrent[b] = colorA;
        ButtonObjs[a].transform.localPosition = new Vector3(posA.x, 0, posA.z);
        ButtonObjs[b].transform.localPosition = new Vector3(posB.x, 0, posB.z);
        BkgdObjs[a].GetComponent<MeshRenderer>().sharedMaterial = ColorMats[3];
        BkgdObjs[b].GetComponent<MeshRenderer>().sharedMaterial = ColorMats[3];
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        SetColors();
        _isAnimating = false;
        if (IsCurrentGridCorrect())
        {
            Debug.LogFormat("[Chromaswapper #{0}] Module solved with color grid {1}.", _moduleId, _colorGridCurrent.Select(i => i.ToString()[0]).Join(""));
            _moduleSolved = true;
            Module.HandlePass();
        }
    }

    private void SetColors()
    {
        for (int i = 0; i < ButtonObjs.Length; i++)
        {
            ButtonObjs[i].GetComponent<MeshRenderer>().material = ColorMats[(int)_colorGridCurrent[i]];
            Texts[i].color = IsCurrentCellCorrect(i) ? new Color32(0, 255, 0, 255) : new Color32(255, 0, 0, 255);
        }
    }

    private bool IsCurrentGridCorrect()
    {
        for (int ix = 0; ix < 36; ix++)
            if (!IsCurrentCellCorrect(ix))
                return false;
        return true;
    }

    private bool IsCurrentCellCorrect(int ix)
    {
        return GetAdjacents(ix).Count(adj => _colorGridCurrent[ix] == _colorGridCurrent[adj]) == _numberGrid[ix];
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} swap a1 b3 [Swap cells at positions A1 and B3.] | Columns are marked A-F. Rows are marked 1-6. | Commands can be chained.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        if (!command.StartsWith("swap "))
            yield break;
        command = command.Substring(5);
        var cmds = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (cmds.Length % 2 != 0)
        {
            yield return "sendtochaterror Command contains an odd number of positions. Ignoring command.";
            yield break;
        }
        var list = new List<int>();
        for (int i = 0; i < cmds.Length; i++)
        {
            var coord = Regex.Match(cmds[i], @"^\s*(?<col>[ABCDEF])(?<row>[123456])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!coord.Success)
                yield break;
            var position = "abcdef".IndexOf(coord.Groups["col"].ToString()) + "123456".IndexOf(coord.Groups["row"].ToString()) * 6;
            list.Add(position);
        }
        yield return null;
        while (_isAnimating)
            yield return null;
        for (int i = 0; i < list.Count; i++)
        {
            while (_isAnimating)
                yield return null;
            ButtonSels[list[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (_isAnimating)
            yield return null;
        for (int i = 0; i < 36; i++)
            for (int j = i; j < 36; j++)
                if (_colorGridCurrent[i] != _colorGridSolution[i] && _colorGridCurrent[j] == _colorGridSolution[i])
                {
                    ButtonSels[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    ButtonSels[j].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    while (_isAnimating)
                        yield return null;
                }
    }
}