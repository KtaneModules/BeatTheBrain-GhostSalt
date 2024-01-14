using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KModkit;
using Rnd = UnityEngine.Random;

public class SatNavScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Image BlackOverlay, CarRend, ArrowRend;
    public GameObject RoadStraight, RoadBend, RoadFinish;
    public Text CardinalText, Instructions;
    public MeshRenderer[] LEDs;

    private KMAudio.KMAudioRef Sound;
    private enum GameState
    {
        Waiting,
        Introduction,
        DoingTurns,
        ShowingCompass,
        WaitingForAnswer,
        ShowingAnswer,
        Solved
    }
    private int Answer, CurrentStage, CurrentState, Highlighted, InitialDirection, Selected;
    private List<int> Turns = new List<int>();
    private bool TurnCompleted;

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
            Buttons[x].OnHighlight += delegate { if (!new[] { (int)GameState.ShowingCompass, (int)GameState.ShowingAnswer, (int)GameState.Solved, (int)GameState.DoingTurns, (int)GameState.Introduction }.Contains(CurrentState)) FindHighlight(x).color = Color.white; Highlighted = x; };
            Buttons[x].OnHighlightEnded += delegate { FindHighlight(x).color = Color.clear; Highlighted = -1; };
            FindHighlight(x).color = Color.clear;
            if (x != 4)
                Buttons[x].OnInteract += delegate { if (CurrentState == (int)GameState.WaitingForAnswer) SubmitAnswer(x); Buttons[x].AddInteractionPunch(); return false; };
        }
        Buttons[4].OnInteract += delegate { if (CurrentState == (int)GameState.Waiting) StartCoroutine(Introduction()); Buttons[4].AddInteractionPunch(); return false; };
        RoadStraight.SetActive(true);
        RoadBend.SetActive(false);
        RoadFinish.SetActive(false);
        CarRend.gameObject.SetActive(false);
        CardinalText.gameObject.SetActive(false);
        ArrowRend.gameObject.SetActive(false);
        BlackOverlay.gameObject.SetActive(false);
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

    void GenerateTurns()
    {
        var noOfTurns = new[] { 4, 6, 8 }[CurrentStage];
        if (CurrentStage == 2)
            InitialDirection = Rnd.Range(0, 4);
        else
            InitialDirection = 0;
        Turns = new List<int>();
        for (int i = 0; i < noOfTurns; i++)
        {
            if (i > 0 && Turns.Last() == 2)
                Turns.Add(new[] { 1, 3 }.PickRandom());
            else
                Turns.Add(Rnd.Range(1, 4));
        }
        Answer = (InitialDirection + Turns.Sum()) % 4;
    }

    void SubmitAnswer(int pos)
    {
        Selected = pos;
        CurrentState = (int)GameState.ShowingAnswer;
        Audio.PlaySoundAtTransform("select", Buttons[pos].transform);
        if (Sound != null)
            Sound.StopSound();
        //StartCoroutine(ShowAnswer());
    }

    void Initialise()
    {
        Instructions.text = "PRESS THE DISPLAY TO START";
        GenerateTurns();
        Debug.LogFormat("[Sat Nav #{0}] The car started facing {1} and made the following turns: {2}.", _moduleID, new[] { "North", "East", "South", "West" }[InitialDirection], Turns.Select(x => new[] { "right", "u-turn", "left" }[x - 1]).Join(", "));
        Debug.LogFormat("[Sat Nav #{0}] This means that the car is now facing {1}.", _moduleID, new[] { "North", "East", "South", "West" }[Answer]);
    }

    private IEnumerator Introduction(float fadeInDuration = 0.65f, float timeBeforeCarEntry = 1f, float carEntryDuration = 0.65f, float cardinalFadeOutDur = 0.35f)
    {
        Sound = Audio.HandlePlaySoundAtTransformWithRef("music", transform, false);
        Buttons[4].gameObject.SetActive(false);
        CardinalText.gameObject.SetActive(true);
        ArrowRend.gameObject.SetActive(true);
        float timer = 0;
        while (timer < fadeInDuration)
        {
            CardinalText.color = ArrowRend.color = Color.Lerp(Color.clear, Color.black, timer / fadeInDuration);
            yield return null;
            timer += Time.deltaTime;
        }
        CardinalText.color = ArrowRend.color = Color.black;
        timer = 0;
        while (timer < timeBeforeCarEntry)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        CarRend.gameObject.SetActive(true);
        timer = 0;
        while (timer < carEntryDuration)
        {
            ArrowRend.color = Color.Lerp(Color.black, Color.clear, timer / carEntryDuration);
            CarRend.color = Color.Lerp(new Color(1, 1, 1, 0.5f), Color.white, timer / carEntryDuration);
            CarRend.transform.localPosition = Vector3.down * Easing.OutSine(timer, 188f, 0f, carEntryDuration);
            yield return null;
            timer += Time.deltaTime;
        }
        ArrowRend.color = Color.clear;
        CarRend.color = Color.white;
        CarRend.transform.localPosition = Vector3.zero;
        timer = 0;
        while (timer < cardinalFadeOutDur)
        {
            yield return null;
            timer += Time.deltaTime;
            CardinalText.color = Color.Lerp(Color.black, Color.clear, timer / cardinalFadeOutDur);
        }
        CardinalText.color = Color.clear;
        StartCoroutine(MakeTurns());
    }

    private IEnumerator MakeTurns()
    {
        foreach (var turn in Turns)
        {
            TurnCompleted = false;
            if (turn == 2)
                StartCoroutine(MakeUTurn());
            else
                StartCoroutine(MakeNinetyTurn(turn == 1));
            while (!TurnCompleted)
                yield return null;
        }
        StartCoroutine(FinishTurns());
        yield return null;
    }

    private IEnumerator MakeNinetyTurn(bool isRightTurn, float timeToCorner = 0.25f, float timeToTurn = 0.45f)
    {
        Audio.PlaySoundAtTransform(!isRightTurn ? "sat nav left" : "sat nav right", transform);
        RoadStraight.SetActive(false);
        RoadBend.SetActive(true);
        RoadBend.transform.localEulerAngles = Vector3.zero;
        RoadBend.transform.localPosition = Vector3.up * 226.5f;
        RoadBend.transform.localScale = new Vector3(isRightTurn ? 1 : -1, 1, 1);
        float timer = 0;
        while (timer < timeToCorner)
        {
            yield return null;
            timer += Time.deltaTime;
            RoadBend.transform.localPosition = Vector3.up * Mathf.Lerp(226.5f, 0, timer / timeToCorner);
        }
        RoadBend.transform.localPosition = Vector3.zero;
        timer = 0;
        while (timer < timeToTurn)
        {
            yield return null;
            timer += Time.deltaTime;
            RoadBend.transform.localEulerAngles = Vector3.forward * Mathf.Lerp(0, isRightTurn ? 90f : -90f, timer / timeToTurn);
        }
        RoadBend.transform.localEulerAngles = Vector3.forward * (isRightTurn ? 90f : -90f);
        timer = 0;
        while (timer < timeToCorner)
        {
            yield return null;
            timer += Time.deltaTime;
            RoadBend.transform.localPosition = Vector3.down * Mathf.Lerp(0, 226.5f, timer / timeToCorner);
        }
        timer = 0;
        while (timer < 0.25f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        RoadStraight.SetActive(true);
        RoadBend.SetActive(false);
        TurnCompleted = true;
    }

    private IEnumerator MakeUTurn(float timeToTurn = 0.65f)
    {
        Audio.PlaySoundAtTransform("sat nav u-turn", transform);
        int randomDir = Rnd.Range(0, 2) == 0 ? 180 : -180;
        float timer = 0;
        while (timer < timeToTurn)
        {
            yield return null;
            timer += Time.deltaTime;
            RoadStraight.transform.localEulerAngles = Vector3.forward * Easing.InOutQuad(timer, 0, randomDir, timeToTurn);
        }
        RoadStraight.transform.localEulerAngles = Vector3.zero;
        timer = 0;
        while (timer < 0.25f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        TurnCompleted = true;
    }

    private IEnumerator FinishTurns(float timeToStop = 0.25f)
    {
        RoadStraight.SetActive(false);
        RoadBend.SetActive(false);
        RoadFinish.SetActive(true);
        RoadFinish.transform.localPosition = Vector3.up * 40f;
        float timer = 0;
        while (timer < timeToStop)
        {
            yield return null;
            timer += Time.deltaTime;
            RoadFinish.transform.localPosition = Vector3.up * Easing.OutSine(timer, 40f, 0f, timeToStop);
        }
        RoadFinish.transform.localPosition = Vector3.zero;
        TurnCompleted = true;
    }

    //private IEnumerator DisplayAnswers(float speed, float pause = 1.4f, float carFade = 0.25f, float answersFade = 0.5f, float answersAnim = 0.25f)
    //{
    //    float timer = 0;
    //    while (timer < pause)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime * speed;
    //    }
    //    CityOverlay.gameObject.SetActive(true);
    //    timer = 0;
    //    while (timer < carFade)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //        CityOverlay.color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / carFade);
    //    }
    //    CarRendFront.gameObject.SetActive(false);
    //    CarRendBack.gameObject.SetActive(false);
    //    CityOverlay.gameObject.SetActive(false);
    //    if (CurrentStage == 2)
    //        for (int i = 0; i < 6; i++)
    //            AnswerPlates[i].sprite = PlateSprites[1];
    //    timer = 0;
    //    while (timer < 0.5f)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //    }
    //    CurrentState = (int)GameState.ShowingCompass;
    //    timer = 0;
    //    while (timer < answersFade)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //        MaskBottom.color = Color.Lerp(Color.white, new Color(1, 1, 1, 0), timer / answersFade);
    //    }
    //    Instructions.text = "WHAT DID YOU SEE?";
    //    MaskBottom.color = Color.white;
    //    MaskBottom.gameObject.SetActive(false);
    //    Buttons[0].transform.parent.gameObject.SetActive(true);
    //    for (int i = 0; i < 6; i++)
    //    {
    //        AnswerLabels[i].color = Color.clear;
    //        AnswerPlates[i].transform.parent.localScale = Vector3.zero;
    //    }
    //    timer = 0;
    //    while (timer < answersAnim)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //        for (int i = 0; i < 6; i++)
    //        {
    //            AnswerLabels[i].color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / answersAnim);
    //            AnswerPlates[i].transform.parent.localScale = new Vector3(1.75f, Easing.OutCirc(timer, 0, 1.75f, answersAnim), 1.75f);
    //        }
    //    }
    //    for (int i = 0; i < 6; i++)
    //    {
    //        AnswerLabels[i].color = Color.white;
    //        AnswerPlates[i].transform.parent.localScale = Vector3.one * 1.75f;
    //    }
    //    if (Highlighted > -1)
    //        FindHighlight(Highlighted).color = Color.white;
    //    CurrentState = (int)GameState.WaitingForAnswer;
    //}

    //private IEnumerator ShowAnswer(float scaleDuration = 1f, float fadeDuration = 0.25f)
    //{
    //    for (int i = 0; i < Buttons.Length; i++)
    //        FindHighlight(i).color = Color.clear;
    //    float timer = 0;
    //    while (timer < 1f)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //    }
    //    Audio.PlaySoundAtTransform("reveal", Buttons[AnswerPos].transform);
    //    if (Selected != AnswerPos)
    //    {
    //        Module.HandleStrike();
    //        Debug.LogFormat("[Catch Me if You Can #{0}] You chose answer {1}, which was incorrect. Strike!", _moduleID, (Selected + 1).ToString());
    //        yield return "strike";
    //    }
    //    else
    //    {
    //        LEDs[CurrentStage].material.color = new Color32(0, 237, 255, 255);
    //        CurrentStage++;
    //        if (CurrentStage == 3)
    //        {
    //            Module.HandlePass();
    //            yield return "solve";
    //            Audio.PlaySoundAtTransform("solve", transform);
    //            CurrentState = (int)GameState.Solved;
    //            Instructions.text = "MODULE SOLVED!";
    //        }
    //        Debug.LogFormat("[Catch Me if You Can #{0}] You chose answer {1}, which was correct. {2}", _moduleID, (Selected + 1).ToString(), CurrentState == (int)GameState.Solved ? "Module solved!" : "Onto the next round!");
    //    }
    //    for (int i = 0; i < 6; i++)
    //    {
    //        if (i == AnswerPos)
    //            StartCoroutine(PlateGlow(i));
    //        else
    //            StartCoroutine(PlateRegress(i));
    //    }
    //    timer = 0;
    //    while (timer < 1.25f)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //    }
    //    if (CurrentState != (int)GameState.Solved)
    //        Instructions.text = "PRESS THE DISPLAY TO START";
    //    else
    //    {
    //        StartCoroutine(SolveAnim());
    //        yield break;
    //    }
    //    Buttons[0].transform.parent.gameObject.SetActive(false);
    //    for (int i = 0; i < 6; i++)
    //    {
    //        PlateGlows[i].color = Color.clear;
    //        AnswerPlates[i].transform.parent.parent.localScale = Vector3.one;
    //    }
    //    MaskBottom.gameObject.SetActive(true);
    //    MaskBottom.color = Color.clear;
    //    GenerateAnswers();
    //    timer = 0;
    //    while (timer < fadeDuration)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;
    //        MaskBottom.color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / fadeDuration);
    //    }
    //    MaskBottom.color = Color.white;
    //    if (CurrentState != (int)GameState.Solved)
    //    {
    //        CurrentState = (int)GameState.Waiting;
    //        Buttons[6].gameObject.SetActive(true);
    //    }
    //}

    private IEnumerator SolveAnim(float duration = 0.5f)
    {
        BlackOverlay.gameObject.SetActive(true);
        BlackOverlay.color = Color.clear;
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            BlackOverlay.color = Color.Lerp(Color.clear, Color.black, timer / duration);
        }
        BlackOverlay.color = Color.black;
    }

    //#pragma warning disable 414
    //    private string TwitchHelpMessage = "Use '!{0} go' to press the display. Use '!{0} 1' to submit answer 1. If something caused the stream to lag, making the numberplate impossible to read, use '!{0} regen' to regenerate the round, with no penalty.";
    //#pragma warning restore 414

    //    IEnumerator ProcessTwitchCommand(string command)
    //    {
    //        command = command.ToLowerInvariant();
    //        var validCommands = new[] { "1", "2", "3", "4", "5", "6", "go" };
    //        if (CurrentState == (int)GameState.WaitingForAnswer && command == "regen")
    //        {
    //            yield return null;
    //            yield return $"sendtochat Regenerating round {CurrentStage + 1}.";
    //            RegenStage();
    //            yield break;
    //        }
    //        else if (command == "regen")
    //        {
    //            yield return "sendtochaterror Cannot regenerate this round yet!";
    //            yield break;
    //        }
    //        if (!validCommands.Contains(command))
    //        {
    //            yield return "sendtochaterror Invalid command.";
    //            yield break;
    //        }
    //        else if (command == "go" && CurrentState != (int)GameState.Waiting)
    //        {
    //            yield return "sendtochaterror Cannot start another round yet!";
    //            yield break;
    //        }
    //        else if (command != "go" && CurrentState != (int)GameState.WaitingForAnswer)
    //        {
    //            yield return "sendtochaterror Cannot select an answer yet!";
    //            yield break;
    //        }
    //        else
    //        {
    //            yield return null;
    //            Buttons[Array.IndexOf(validCommands, command)].OnInteract();
    //        }
    //    }
    //    IEnumerator TwitchHandleForcedSolve()
    //    {
    //        while (CurrentState != (int)GameState.Solved)
    //        {
    //            switch (CurrentState)
    //            {
    //                case (int)GameState.Waiting:
    //                    Buttons[6].OnInteract();
    //                    break;
    //                case (int)GameState.WaitingForAnswer:
    //                    Buttons[AnswerPos].OnInteract();
    //                    break;
    //                default:
    //                    yield return true;
    //                    break;
    //            }
    //        }
    //    }
}