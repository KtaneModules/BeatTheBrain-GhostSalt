using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KModkit;
using Rnd = UnityEngine.Random;

public class WinningLineScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Image[] MainGridSymbols;
    public Image[] AnswerGridSymbols;
    public Image MainGrid, Overlay;
    public Image[] AnswerGrids;
    public Sprite[] Symbols;
    public Text Instructions;
    public Text[] AnswerGridTexts;
    public MeshRenderer[] LEDs;

    private KMAudio.KMAudioRef Sound;
    private enum GameState
    {
        Waiting,
        ShowingGrid,
        ShowingAnswers,
        WaitingForAnswer,
        ShowingAnswer,
        Solved
    }

    private static readonly int[] AnswerIndexes = new[] { 0, 0, 1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 4, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7 };
    private int[] Answers;
    private int AnswerPos, CurrentStage, CurrentState, Highlighted, Selected;
    private static readonly string[] AllGrids = new[] { "XXXX...X.", "XXXX....X", "X..XXX.X.", ".X.XXXX..", ".X.XXX..X", "..XXXX.X.", "X....XXXX", ".X...XXXX", "XX.X.XX..", "XX.X..X.X", "XX..XX.X.", ".XXXX..X.", ".X.XX..XX", ".X..XXXX.", "X.X..X.XX", "..XX.X.XX", "XX.XX...X", "XX..XX..X", "XX..X.X.X", "X.XXX...X", "X.X.X..XX", "X..XX..XX", "X...XXX.X", "X...XX.XX", "X.X.XXX..", "X.X.X.XX.", ".XXXX.X..", ".XX.XXX..", ".XX.X.X.X", "..XXX.XX.", "..XXX.X.X", "..X.XXXX.", "XX..X...X", "X.X.X...X", "X..XX...X", "X...XX..X", "X...X.X.X", "X...X..XX", "X.X.X.X..", ".XX.X.X..", "..XXX.X..", "..X.XXX..", "..X.X.XX.", "..X.X.X.X" };
    private static readonly string[] Lines = new[] { "XXX......", "...XXX...", "......XXX", "X..X..X..", ".X..X..X.", "..X..X..X", "X...X...X", "..X.X.X.." };
    private string AnswerDisplayable, Grid;

    private int[] GetAnswers()
    {
        return Enumerable.Range(0, 8).Where(x => CurrentStage == 2 ? true : CurrentStage == 1 ? !new[] { 3, 4, 5 }.Contains(x) : !new[] { 0, 1, 2 }.Contains(x)).ToArray().Shuffle().Take(4).ToArray();
    }

    private string GenerateGrid()
    {
        return AllGrids.Where(x => Lines[Answers[AnswerPos]].Where((y, ix) => y == '.' || x[ix] == 'X').Count() == 9).PickRandom();
    }

    private string MakeAnswerDisplayable(int answer)
    {
        return Lines[answer];
    }

    private Image FindHighlight(int pos)
    {
        return Buttons[pos].GetComponentsInChildren<Image>().Where(x => x.name == "Highlight").First();
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnHighlight += delegate { if (CurrentState != (int)GameState.ShowingAnswers && CurrentState != (int)GameState.ShowingAnswer && CurrentState != (int)GameState.Solved) FindHighlight(x).color = Color.white; Highlighted = x; };
            Buttons[x].OnHighlightEnded += delegate { FindHighlight(x).color = Color.clear; Highlighted = -1; };
            FindHighlight(x).color = Color.clear;
            if (x != 4)
                Buttons[x].OnInteract += delegate { if (CurrentState == (int)GameState.WaitingForAnswer) SubmitAnswer(x); return false; };
        }
        Buttons[4].OnInteract += delegate { if (CurrentState == (int)GameState.Waiting) StartRound(); return false; };
        ArrangeSymbols();
    }

    // Use this for initialization
    void Start()
    {
        Initialise();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void StartRound()
    {
        Buttons[4].AddInteractionPunch();
        CurrentState = (int)GameState.ShowingGrid;
        Buttons[4].gameObject.SetActive(false);
        Instructions.text = "WATCH THE GAME OF NOUGHTS AND\nCROSSES";
        Sound = Audio.HandlePlaySoundAtTransformWithRef("music", transform, false);
        Debug.LogFormat("[Winning Line #{0}] Starting round {1}.", _moduleID, CurrentStage + 1);
        Debug.LogFormat("[Winning Line #{0}] The winning line was answer {1}.", _moduleID, new[] { "A", "B", "C", "D" }[AnswerPos]);
        StartCoroutine(ShowGrid());
    }

    void SubmitAnswer(int pos)
    {
        Buttons[pos].AddInteractionPunch();
        Selected = pos;
        CurrentState = (int)GameState.ShowingAnswer;
        Audio.PlaySoundAtTransform("select", Buttons[pos].transform);
        if (Sound != null)
            Sound.StopSound();
        StartCoroutine(ShowAnswer());
    }

    void ArrangeSymbols()
    {
        AnswerPos = Rnd.Range(0, 4);
        Answers = GetAnswers();
        Grid = GenerateGrid();
        var answerHasO = Rnd.Range(0, 2) == 0;
        if (answerHasO)
            Grid = Grid.Select(x => x == 'X' ? '.' : 'X').Join("");
        for (int i = 0; i < MainGridSymbols.Length; i++)
            MainGridSymbols[i].sprite = Symbols[Grid[i] == 'X' ? 0 : 1];
        var answersWithO = new List<int>();
        if (answerHasO)
        {
            answersWithO.Add(AnswerPos);
            answersWithO.Add(Enumerable.Range(0, 4).Where(x => x != AnswerPos).PickRandom());
        }
        else
            answersWithO.AddRange(Enumerable.Range(0, 4).Where(x => x != AnswerPos).ToArray().Shuffle().Take(2));
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 9; j++)
                AnswerGridSymbols[(i * 9) + j].sprite = Symbols[MakeAnswerDisplayable(Answers[i])[j] == '.' ? 2 : answersWithO.Contains(i) ? 1 : 0];
    }

    void Initialise()
    {
        for (int i = 0; i < 9; i++)
            MainGridSymbols[i].gameObject.SetActive(false);
        for (int i = 0; i < 4; i++)
            AnswerGrids[i].transform.localScale = Vector3.zero;
        Instructions.text = "PRESS THE DISPLAY TO START";
        for (int i = 0; i < Buttons.Length - 1; i++)
            Buttons[i].transform.localScale = Vector3.zero;
        Overlay.color = Color.clear;
    }

    private IEnumerator ShowAnswer(float scaleDuration = 1f, float fadeDuration = 0.5f)
    {
        for (int i = 0; i < Buttons.Length; i++)
            FindHighlight(i).color = Color.clear;
        float timer = 0;
        while (timer < 1f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Audio.PlaySoundAtTransform("reveal", Buttons[AnswerPos].transform);
        if (Selected != AnswerPos)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Winning Line #{0}] You chose answer {1}, which was incorrect. Strike!", _moduleID, new[] { "A", "B", "C", "D" }[Selected]);
            yield return "strike";
        }
        else
        {
            LEDs[CurrentStage].material.color = new Color32(0, 237, 255, 255);
            CurrentStage++;
            if (CurrentStage == 3)
            {
                Module.HandlePass();
                yield return "solve";
                Audio.PlaySoundAtTransform("solve", transform);
                CurrentState = (int)GameState.Solved;
                Instructions.text = "MODULE SOLVED!";
            }
            Debug.LogFormat("[Winning Line #{0}] You chose answer {1}, which was correct. {2}", _moduleID, new[] { "A", "B", "C", "D" }[Selected], CurrentState == (int)GameState.Solved ? "Module solved!" : "Onto the next round!");
        }
        for (int i = 0; i < 4; i++)
            if (i != AnswerPos)
                AnswerGrids[i].transform.localScale = Vector3.zero;
        var init = AnswerGrids[AnswerPos].transform.localPosition;
        timer = 0;
        while (timer < scaleDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            AnswerGrids[AnswerPos].transform.localPosition = Vector3.Lerp(init, new Vector3(150, 0, 0), timer / scaleDuration);
            AnswerGrids[AnswerPos].transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 2, timer / scaleDuration);
        }
        AnswerGrids[AnswerPos].transform.localPosition = new Vector3(150, 0, 0);
        AnswerGrids[AnswerPos].transform.localScale = Vector3.one * 2;
        timer = 0;
        while (timer < 0.5f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        if (CurrentState != (int)GameState.Solved)
            Instructions.text = "PRESS THE DISPLAY TO START";
        else
        {
            StartCoroutine(SolveAnim());
            yield break;
        }
        AnswerGrids[AnswerPos].transform.localScale = Vector3.zero;
        AnswerGrids[AnswerPos].transform.localPosition = init;
        timer = 0;
        while (timer < fadeDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            MainGrid.color = Color.Lerp(new Color32(255, 125, 45, 0), new Color32(255, 125, 45, 255), timer / fadeDuration);
        }
        MainGrid.color = new Color32(255, 125, 45, 255);
        if (CurrentState != (int)GameState.Solved)
        {
            CurrentState = (int)GameState.Waiting;
            ArrangeSymbols();
            Buttons[4].gameObject.SetActive(true);
        }
    }

    private IEnumerator SolveAnim(float duration = 0.5f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Overlay.color = Color.Lerp(Color.clear, Color.black, timer / duration);
        }
        Overlay.color = Color.black;
    }

    private IEnumerator ShowGrid(float outDuration = 0.75f)
    {
        var order = new List<List<int>>();
        var order0 = new List<List<int>>() { new List<int>() { 0, 1, 2 }, new List<int>() { 3, 4, 5 }, new List<int>() { 6, 7, 8 } };
        var order1 = new List<List<int>>() { new List<int>() { 0, 3, 6 }, new List<int>() { 1, 4, 7 }, new List<int>() { 2, 5, 8 } };
        switch (CurrentStage)
        {
            case 0:
                order = order0.ToList();
                break;
            case 1:
                order = order1.ToList();
                break;
            default:
                restart:
                var temp = Enumerable.Range(0, 9).ToArray().Shuffle();
                if (temp.Where((x, ix) => order0[ix / 3][x % 3] == x || order1[ix / 3][x % 3] == x).Count() == 9)
                    goto restart;
                order = new List<List<int>>() { temp.Take(3).ToList(), temp.Skip(3).Take(3).ToList(), temp.Skip(6).Take(3).ToList() };
                break;
        }
        float timer = 0;
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(ShowTriplet(order[i], i == 2));
            timer = 0;
            while (timer < 1f - CurrentStage * 0.175f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        timer = 0;
        var init = MainGrid.color;
        while (timer < outDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            MainGrid.color = Color.Lerp(init, new Color(init.r, init.g, init.b, 0), timer / outDuration);
        }
        MainGrid.color = Color.clear;
        StartCoroutine(ShowAnswers());
    }

    private IEnumerator ShowTriplet(List<int> order, bool isLast, float inDuration = 0.25f, float outDuration = 0.75f)
    {
        outDuration -= CurrentStage * 0.175f;
        foreach (var ix in order)
        {
            MainGridSymbols[ix].gameObject.SetActive(true);
            MainGridSymbols[ix].rectTransform.sizeDelta = new Vector2(40, 40);
            MainGridSymbols[ix].color = Color.clear;
        }
        float timer = 0;
        while (timer < inDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            foreach (var ix in order)
            {
                MainGridSymbols[ix].rectTransform.sizeDelta = new Vector2(1, 1) * Mathf.Lerp(40f, 80f, timer / inDuration);
                MainGridSymbols[ix].color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / inDuration);
            }
        }
        foreach (var ix in order)
        {
            MainGridSymbols[ix].rectTransform.sizeDelta = new Vector2(80, 80);
            MainGridSymbols[ix].color = Color.white;
        }
        timer = 0;
        while (timer < outDuration / 2)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        timer = 0;
        while (timer < outDuration / 2)
        {
            yield return null;
            timer += Time.deltaTime;
            foreach (var ix in order)
                MainGridSymbols[ix].color = new Color(1, 1, 1, Easing.InSine(timer, 1, 0, outDuration / 2));
        }
        foreach (var ix in order)
        {
            MainGridSymbols[ix].color = Color.white;
            MainGridSymbols[ix].gameObject.SetActive(false);
        }
    }

    private IEnumerator ShowAnswers(float fadeDuration = 0.25f, float inDuration = 0.25f)
    {
        CurrentState = (int)GameState.ShowingAnswers;
        Instructions.text = "WHICH FORMATION WON THE GAME?";
        for (int i = 0; i < 4; i++)
        {
            AnswerGrids[i].transform.localScale = Vector3.one;
            for (int j = 0; j < 9; j++)
                AnswerGridSymbols[(i * 9) + j].gameObject.SetActive(false);
            var init = AnswerGrids[i].color;
            AnswerGrids[i].color = AnswerGridTexts[i].color = Color.clear;
            Buttons[i].transform.localScale = Vector3.one;
            float timer = 0;
            while (timer < fadeDuration)
            {
                yield return null;
                timer += Time.deltaTime;
                AnswerGrids[i].color = Color.Lerp(new Color(init.r, init.g, init.b, 0), init, timer / fadeDuration);
                AnswerGridTexts[i].color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / fadeDuration);
            }
            AnswerGrids[i].color = init;
            AnswerGridTexts[i].color = Color.white;
            timer = 0;
            while (timer < 0.25f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            for (int j = 0; j < 9; j++)
            {
                AnswerGridSymbols[(i * 9) + j].gameObject.SetActive(true);
                AnswerGridSymbols[(i * 9) + j].color = Color.clear;
            }
            timer = 0;
            while (timer < inDuration)
            {
                yield return null;
                timer += Time.deltaTime;
                for (int j = 0; j < 9; j++)
                {
                    AnswerGridSymbols[(i * 9) + j].rectTransform.sizeDelta = new Vector2(1, 1) * Mathf.Lerp(40, 80, timer / inDuration);
                    AnswerGridSymbols[(i * 9) + j].color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / inDuration);
                }
            }
            for (int j = 0; j < 9; j++)
            {
                AnswerGridSymbols[(i * 9) + j].rectTransform.sizeDelta = new Vector2(80, 80);
                AnswerGridSymbols[(i * 9) + j].color = Color.white;
            }
        }
        CurrentState = (int)GameState.WaitingForAnswer;
        if (Highlighted > -1)
            FindHighlight(Highlighted).color = Color.white;
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} go' to press the display. Use '!{0} A' to submit answer A.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var validCommands = new[] { "a", "b", "c", "d", "go" };
        if (!validCommands.Contains(command))
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
        else if (command == "go" && CurrentState != (int)GameState.Waiting)
        {
            yield return "sendtochaterror Cannot start another round yet!";
            yield break;
        }
        else if (command != "go" && CurrentState != (int)GameState.WaitingForAnswer)
        {
            yield return "sendtochaterror Cannot select an answer yet!";
            yield break;
        }
        else
        {
            yield return null;
            Buttons[Array.IndexOf(validCommands, command)].OnInteract();
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        while (CurrentState != (int)GameState.Solved)
        {
            switch (CurrentState)
            {
                case (int)GameState.Waiting:
                    Buttons[4].OnInteract();
                    break;
                case (int)GameState.WaitingForAnswer:
                    Buttons[AnswerPos].OnInteract();
                    break;
                default:
                    yield return true;
                    break;
            }
        }
    }
}