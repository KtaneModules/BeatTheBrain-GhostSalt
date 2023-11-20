using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KModkit;
using Rnd = UnityEngine.Random;
using NUnit.Framework.Constraints;

public class SatNavScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Image BlackOverlay, CarRend, ArrowRend;
    public GameObject RoadStraight, RoadBend;
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
    private int AnswerPos, CurrentStage, CurrentState, Highlighted, Selected;
    private List<string> Answers = new List<string>();

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
        Buttons[0].transform.parent.gameObject.SetActive(false);
    }

    private IEnumerator Introduction(float fadeInDuration = 0.65f, float timeBeforeCarEntry = 1f, float carEntryDuration = 0.65f)
    {
        Sound = Audio.HandlePlaySoundAtTransformWithRef("music", transform, false);
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