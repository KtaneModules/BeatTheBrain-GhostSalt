using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KModkit;
using Rnd = UnityEngine.Random;

public class CatchMeIfYouCanScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Image[] PlateGlows;
    public Image BlackOverlay, CityOverlay, CarRendFront, CarRendBack, CountdownRend, MaskBottom;
    public Image[] AnswerPlates;
    public Sprite[] PlateSprites;
    public Text Instructions;
    public Text[] AnswerTexts, AnswerLabels;
    public MeshRenderer[] LEDs;
    public Sprite[] CountdownSprites;

    private KMAudio.KMAudioRef Sound;
    private enum GameState
    {
        Waiting,
        CountingDown,
        CarDriving,
        ShowingAnswers,
        WaitingForAnswer,
        ShowingAnswer,
        Solved
    }
    private int AnswerPos, CurrentStage, CurrentState, Highlighted, Selected;
    private List<string> Answers = new List<string>();

    private string GenerateNumberplate()
    {
        var lettersInitial = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y' }.Shuffle().Join("");
        var lettersFinal = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' }.Shuffle().Join("");
        return lettersInitial.Substring(0, 2) + Rnd.Range(0, 100).ToString("00") + " " + lettersFinal.Substring(0, 3);
    }

    private struct MutatedPlate
    {
        public string Numberplate;
        public int MutationIx;
        public char MutatedChar;

        public MutatedPlate(string numberplate, int mutationIx, char mutatedChar)
        {
            Numberplate = numberplate;
            MutationIx = mutationIx;
            MutatedChar = mutatedChar;
        }
    }

    void GenerateAnswers()
    {
        Answers = new List<string>();
        var correct = GenerateNumberplate();
        var answerBase = MutateAnswer(correct);
        var commonIx = Enumerable.Range(0, 7).Where(x => x != answerBase.MutationIx).PickRandom();
        Answers.Add(correct);
        for (int i = 0; i < 7; i++)
            if (i != commonIx && i != answerBase.MutationIx)
                Answers.Add(MutateAnswer(answerBase.Numberplate, i).Numberplate);
        Answers.Shuffle();
        AnswerPos = Answers.IndexOf(correct);
        for (int i = 0; i < 6; i++)
            AnswerTexts[i].text = Answers[i];
    }

    private MutatedPlate MutateAnswer(string numberplate, int ix = -1)
    {
        if (ix == -1)
            ix = Rnd.Range(0, 7);
        var oldIx = ix;
        if (ix > 3)
            ix++;
        char mutatedLetter;
        if (ix < 2)
            mutatedLetter = "ABCDEFGHIJKLMNOPRSTUVWXY".Except(numberplate).PickRandom();
        else if (ix > 4)
            mutatedLetter = "ABCDEFGHJKLMNOPRSTUVWXYZ".Except(numberplate).PickRandom();
        else
            mutatedLetter = "0123456789".Except(numberplate).PickRandom();
        return new MutatedPlate(numberplate.Substring(0, ix) + mutatedLetter + numberplate.Substring(ix + 1), oldIx, mutatedLetter);
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
            Buttons[x].OnHighlight += delegate { if (!new[] { (int)GameState.ShowingAnswers, (int)GameState.ShowingAnswer, (int)GameState.Solved, (int)GameState.CarDriving, (int)GameState.CountingDown }.Contains(CurrentState)) FindHighlight(x).color = Color.white; Highlighted = x; };
            Buttons[x].OnHighlightEnded += delegate { FindHighlight(x).color = Color.clear; Highlighted = -1; };
            FindHighlight(x).color = Color.clear;
            if (x != 6)
                Buttons[x].OnInteract += delegate { if (CurrentState == (int)GameState.WaitingForAnswer) SubmitAnswer(x); Buttons[x].AddInteractionPunch(); return false; };
        }
        Buttons[6].OnInteract += delegate { if (CurrentState == (int)GameState.Waiting) StartCoroutine(Countdown()); Buttons[6].AddInteractionPunch(); return false; };
        CountdownRend.sprite = CountdownSprites[3];
        BlackOverlay.gameObject.SetActive(false);
        CityOverlay.gameObject.SetActive(false);
        CarRendFront.gameObject.SetActive(false);
        CarRendBack.gameObject.SetActive(false);
        for (int i = 0; i < 6; i++)
            PlateGlows[i].color = Color.clear;
        GenerateAnswers();
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
        StartCoroutine(ShowAnswer());
    }

    void Initialise()
    {
        Instructions.text = "PRESS THE DISPLAY TO START";
        Buttons[0].transform.parent.localScale = Vector3.zero;
    }

    void RegenStage()   //for TP
    {
        MaskBottom.color = Color.white;
        CurrentState = (int)GameState.Waiting;
        Buttons[6].gameObject.SetActive(true);
        Buttons[0].transform.parent.localScale = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            PlateGlows[i].color = Color.clear;
            AnswerPlates[i].transform.parent.parent.localScale = Vector3.one;
        }
        MaskBottom.gameObject.SetActive(true);
        Instructions.text = "PRESS THE DISPLAY TO START";
        GenerateAnswers();
    }

    private IEnumerator CarAnimInward(float speed, float driveDuration = 1.25f)
    {
        CarRendFront.gameObject.SetActive(true);
        CarRendFront.transform.localScale = Vector3.one * 0.25f;
        CityOverlay.gameObject.SetActive(true);
        CarRendFront.GetComponentInChildren<Text>().text = Answers[AnswerPos];
        StartCoroutine(DisplayAnswers(speed, driveDuration - 0.25f));
        StartCoroutine(CityOverlayReveal());
        float timer = 0;
        while (timer < driveDuration)
        {
            yield return null;
            timer += Time.deltaTime * speed;
            var scale = Easing.InCirc(timer, 0.25f, 10f, driveDuration);
            CarRendFront.transform.localScale = Vector3.one * (double.IsNaN(scale) ? 10f : scale);
            var x = Easing.InCirc(timer, -12, -1f, driveDuration);
            var y = Easing.InCirc(timer, 57.2f, 838f, driveDuration);
            CarRendFront.transform.localPosition = new Vector3((double.IsNaN(x) ? -1f : x), (double.IsNaN(y) ? 838f : y), 0);
        }
        CarRendFront.gameObject.SetActive(false);
    }

    private IEnumerator CarAnimOutward(float driveDuration = 1f)
    {
        CarRendBack.gameObject.SetActive(true);
        CarRendBack.transform.localScale = Vector3.one * 10f;
        CityOverlay.gameObject.SetActive(true);
        CarRendBack.GetComponentInChildren<Text>().text = Answers[AnswerPos];
        StartCoroutine(DisplayAnswers(1f, driveDuration - 0.25f));
        StartCoroutine(CityOverlayReveal());
        float timer = 0;
        while (timer < driveDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            var scale = Easing.OutCirc(timer, 10f, 0.25f, driveDuration);
            CarRendBack.transform.localScale = Vector3.one * (double.IsNaN(scale) ? 10f : scale);
        }
        CarRendBack.gameObject.SetActive(false);
    }

    private IEnumerator CityOverlayReveal(float duration = 0.25f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            CityOverlay.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0, timer / 0.25f));
        }
        CityOverlay.color = Color.clear;
    }

    private IEnumerator Countdown()
    {
        Debug.LogFormat("[Catch Me if You Can #{0}] Starting round {1}.", _moduleID, CurrentStage + 1);
        Debug.LogFormat("[Catch Me if You Can #{0}] The numberplate is {1}, so the correct answer is {2}.", _moduleID, Answers[AnswerPos], (AnswerPos + 1).ToString());
        Sound = Audio.HandlePlaySoundAtTransformWithRef($"cmiyc music {CurrentStage + 1}", transform, false);
        Instructions.text = "WATCH CAREFULLY";
        Buttons[6].gameObject.SetActive(false);
        CurrentState = (int)GameState.CountingDown;
        float timer = 0;
        for (int i = 0; i < 4; i++)
        {
            CountdownRend.sprite = CountdownSprites[i];
            timer = 0;
            while (timer < (i == 3 && CurrentStage != 2 ? 0.4f : 0.8f))
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        CurrentState = (int)GameState.CarDriving;
        if (CurrentStage < 2)
            StartCoroutine(CarAnimInward(new[] { 1f, 1.3f }[CurrentStage]));
        else
            StartCoroutine(CarAnimOutward());
    }

    private IEnumerator DisplayAnswers(float speed, float pause = 1.4f, float carFade = 0.25f, float answersFade = 0.5f, float answersAnim = 0.25f)
    {
        float timer = 0;
        while (timer < pause)
        {
            yield return null;
            timer += Time.deltaTime * speed;
        }
        CityOverlay.gameObject.SetActive(true);
        timer = 0;
        while (timer < carFade)
        {
            yield return null;
            timer += Time.deltaTime;
            CityOverlay.color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / carFade);
        }
        CarRendFront.gameObject.SetActive(false);
        CarRendBack.gameObject.SetActive(false);
        CityOverlay.gameObject.SetActive(false);
        if (CurrentStage == 2)
            for (int i = 0; i < 6; i++)
                AnswerPlates[i].sprite = PlateSprites[1];
        timer = 0;
        while (timer < 0.5f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        CurrentState = (int)GameState.ShowingAnswers;
        timer = 0;
        while (timer < answersFade)
        {
            yield return null;
            timer += Time.deltaTime;
            MaskBottom.color = Color.Lerp(Color.white, new Color(1, 1, 1, 0), timer / answersFade);
        }
        Instructions.text = "WHAT DID YOU SEE?";
        MaskBottom.color = Color.white;
        MaskBottom.gameObject.SetActive(false);
        Buttons[0].transform.parent.gameObject.transform.localScale = Vector3.one * 0.14f;
        for (int i = 0; i < 6; i++)
        {
            AnswerLabels[i].color = Color.clear;
            AnswerPlates[i].transform.parent.localScale = Vector3.zero;
        }
        timer = 0;
        while (timer < answersAnim)
        {
            yield return null;
            timer += Time.deltaTime;
            for (int i = 0; i < 6; i++)
            {
                AnswerLabels[i].color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / answersAnim);
                AnswerPlates[i].transform.parent.localScale = new Vector3(1.75f, Easing.OutCirc(timer, 0, 1.75f, answersAnim), 1.75f);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            AnswerLabels[i].color = Color.white;
            AnswerPlates[i].transform.parent.localScale = Vector3.one * 1.75f;
        }
        if (Highlighted > -1)
            FindHighlight(Highlighted).color = Color.white;
        CurrentState = (int)GameState.WaitingForAnswer;
    }

    private IEnumerator ShowAnswer(float scaleDuration = 1f, float fadeDuration = 0.25f)
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
            Debug.LogFormat("[Catch Me if You Can #{0}] You chose answer {1}, which was incorrect. Strike!", _moduleID, (Selected + 1).ToString());
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
            Debug.LogFormat("[Catch Me if You Can #{0}] You chose answer {1}, which was correct. {2}", _moduleID, (Selected + 1).ToString(), CurrentState == (int)GameState.Solved ? "Module solved!" : "Onto the next round!");
        }
        for (int i = 0; i < 6; i++)
        {
            if (i == AnswerPos)
                StartCoroutine(PlateGlow(i));
            else
                StartCoroutine(PlateRegress(i));
        }
        timer = 0;
        while (timer < 1.25f)
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
        Buttons[0].transform.parent.localScale = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            PlateGlows[i].color = Color.clear;
            AnswerPlates[i].transform.parent.parent.localScale = Vector3.one;
        }
        MaskBottom.gameObject.SetActive(true);
        MaskBottom.color = Color.clear;
        GenerateAnswers();
        timer = 0;
        while (timer < fadeDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            MaskBottom.color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / fadeDuration);
        }
        MaskBottom.color = Color.white;
        if (CurrentState != (int)GameState.Solved)
        {
            CurrentState = (int)GameState.Waiting;
            Buttons[6].gameObject.SetActive(true);
        }
    }

    private IEnumerator PlateGlow(int pos, float duration = 0.25f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            PlateGlows[pos].color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, timer / duration);
        }
        PlateGlows[pos].color = Color.white;
    }

    private IEnumerator PlateRegress(int pos, float duration = 0.25f, float from = 1f, float to = .75f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            AnswerPlates[pos].transform.parent.parent.localScale = Vector3.one * Mathf.Lerp(from, to, timer / duration);
        }
        AnswerPlates[pos].transform.parent.parent.localScale = Vector3.one * to;
    }

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

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} go' to press the display. Use '!{0} 1' to submit answer 1. If something caused the stream to lag, making the numberplate impossible to read, use '!{0} regen' to regenerate the round, with no penalty.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var validCommands = new[] { "1", "2", "3", "4", "5", "6", "go" };
        if (CurrentState == (int)GameState.WaitingForAnswer && command == "regen")
        {
            yield return null;
            yield return $"sendtochat Regenerating round {CurrentStage + 1}.";
            RegenStage();
            yield break;
        }
        else if (command == "regen")
        {
            yield return "sendtochaterror Cannot regenerate this round yet!";
            yield break;
        }
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
                    Buttons[6].OnInteract();
                    break;
                case (int)GameState.WaitingForAnswer:
                    Buttons[AnswerPos].OnInteract();
                    break;
                default:
                    break;
            }
            yield return true;
        }
    }
}