using System.Collections;
using UnityEngine;
using TMPro;

public class TypeWriterEffect : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public float typingSpeed = 0.03f;

    public bool IsTyping => _typingRoutine != null;

    private Coroutine _typingRoutine;
    private TextMeshProUGUI _currentTarget;
    private string _currentLine;
    private System.Action _onCompleted;

    /// <summary>
    /// Starts typing the given line into the target. If another line is currently typing, it is stopped.
    /// </summary>
    public void Begin(TextMeshProUGUI target, string line, System.Action onCompleted = null)
    {
        if (target == null)
            return;

        StopTypingInternal(invokeCompleted: false);
        _currentTarget = target;
        _currentLine = line ?? "";
        _onCompleted = onCompleted;
        _typingRoutine = StartCoroutine(TypeRoutine());
    }

    /// <summary>
    /// Immediately completes the current line (if typing).
    /// </summary>
    public void CompleteInstantly()
    {
        if (_typingRoutine == null)
            return;

        if (_currentTarget != null)
            _currentTarget.text = _currentLine ?? "";

        StopTypingInternal(invokeCompleted: true);
    }

    public IEnumerator ShowText(string line)
    {
        if (dialogueText == null)
            yield break;

        yield return ShowText(dialogueText, line);
    }

    public IEnumerator ShowText(TextMeshProUGUI target, string line)
    {
        if (target == null)
            yield break;

        target.text = "";
        if (string.IsNullOrEmpty(line))
            yield break;

        foreach (char letter in line)
        {
            target.text += letter;
            // Dialogue freezes the game with Time.timeScale=0, so use unscaled time for typing.
            yield return new WaitForSecondsRealtime(typingSpeed);
        }
    }

    private IEnumerator TypeRoutine()
    {
        if (_currentTarget == null)
        {
            StopTypingInternal(invokeCompleted: false);
            yield break;
        }

        _currentTarget.text = "";
        if (string.IsNullOrEmpty(_currentLine))
        {
            StopTypingInternal(invokeCompleted: true);
            yield break;
        }

        foreach (char letter in _currentLine)
        {
            _currentTarget.text += letter;
            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        StopTypingInternal(invokeCompleted: true);
    }

    private void StopTypingInternal(bool invokeCompleted)
    {
        if (_typingRoutine != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }

        var cb = _onCompleted;
        _onCompleted = null;
        _currentTarget = null;
        _currentLine = null;

        if (invokeCompleted)
            cb?.Invoke();
    }
}