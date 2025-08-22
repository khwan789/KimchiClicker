using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class HoldToRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Timing")]
    public float initialDelay = 0.35f;     // delay before repeating starts
    public float repeatInterval = 0.10f;   // first repeat gap
    public float acceleration = 0.90f;     // interval *= acceleration each step
    public float minInterval = 0.03f;      // floor for the interval

    [Header("Behavior")]
    public bool fireOnPointerDown = true;  // do one step immediately on press
    public UnityEvent onStep;              // assign what to do per step

    Coroutine _co;
    bool _holding;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!GetComponent<Button>().interactable) return;

        _holding = true;
        if (fireOnPointerDown) onStep?.Invoke();
        _co = StartCoroutine(RepeatLoop());
    }

    public void OnPointerUp(PointerEventData eventData) => CancelHold();
    public void OnPointerExit(PointerEventData eventData) => CancelHold();

    IEnumerator RepeatLoop()
    {
        yield return new WaitForSecondsRealtime(initialDelay);
        float gap = repeatInterval;
        while (_holding)
        {
            onStep?.Invoke();
            yield return new WaitForSecondsRealtime(gap);
            gap = Mathf.Max(minInterval, gap * acceleration);
        }
    }

    public void CancelHold()
    {
        _holding = false;
        if (_co != null) StopCoroutine(_co);
        _co = null;
    }
}
